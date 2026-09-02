using Sandbox.Npcs;
using Sandbox.UI;

public sealed partial class GameManager : GameObjectSystem<GameManager>, Component.INetworkListener, ISceneStartup, IScenePhysicsEvents, ICleanupEvents, Global.ISaveEvents
{
	public GameManager( Scene scene ) : base( scene )
	{
	}

	void ISceneStartup.OnHostInitialize()
	{
		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig() { Privacy = Sandbox.Network.LobbyPrivacy.Public, MaxPlayers = 32, Name = "Sandbox", DestroyWhenHostLeaves = true } );
		}
	}

	internal void Notify( string text )
	{
		Assert.True( Networking.IsHost, "Only the host can send notifications" );
		NotifyRpc( text );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void NotifyRpc( string text )
	{
		Sandbox.Platform.Chat.AddText( text );
	}

	void Component.INetworkListener.OnActive( Connection channel )
	{
		channel.CanSpawnObjects = false;

		var playerData = CreatePlayerInfo( channel );
		SpawnPlayer( playerData );
		CheckConnectionAchievement( channel );
		CheckFriendsOnlineStat();
	}

	/// <summary>
	/// Called when someone leaves the server. This will only be called for the host.
	/// </summary>
	void Component.INetworkListener.OnDisconnected( Connection channel )
	{
		var pd = PlayerData.For( channel );
		if ( pd is not null )
		{
			pd.GameObject.Destroy();
		}

		UndoSystem.Current?.RemovePlayer( channel.SteamId );
	}

	private PlayerData CreatePlayerInfo( Connection channel )
	{
		var existingPlayerInfo = PlayerData.For( channel );
		if ( existingPlayerInfo.IsValid() )
			return existingPlayerInfo;

		var go = new GameObject( true, $"PlayerInfo - {channel.DisplayName}" );
		var data = go.AddComponent<PlayerData>();

		go.NetworkSpawn( channel );
		go.Network.SetOwnerTransfer( OwnerTransfer.Fixed );

		return data;
	}

	internal void SpawnPlayer( Connection connection ) => SpawnPlayer( PlayerData.For( connection ) );

	internal void SpawnPlayer( PlayerData playerData )
	{
		Assert.NotNull( playerData, "PlayerData is null" );
		Assert.True( Networking.IsHost, $"Client tried to SpawnPlayer: {playerData.Network.Owner?.DisplayName}" );

		// does this connection already have a player?
		if ( Scene.GetAll<Player>().Any( x => x.Network.Owner == playerData.Network.Owner ) )
			return;

		// Find a spawn location for this player
		var startLocation = FindSpawnLocation().WithScale( 1 );

		// Fire pre-respawn event — listeners can modify spawn location
		var respawnEvent = new PlayerRespawnEvent { PlayerData = playerData, SpawnLocation = startLocation };
		Global.IPlayerEvents.Post( x => x.OnPlayerRespawning( respawnEvent ) );
		startLocation = respawnEvent.SpawnLocation;

		// Spawn this object and make the client the owner
		var playerGo = GameObject.Clone( "/prefabs/engine/player.prefab", new CloneConfig { Name = playerData.Network.Owner?.DisplayName, StartEnabled = false, Transform = startLocation } );

		var player = playerGo.Components.Get<Player>( true );
		player.PlayerData = playerData;

		var owner = playerData.Network.Owner;
		playerGo.NetworkSpawn( owner );

		Local.IPlayerEvents.PostToGameObject( player.GameObject, x => x.OnSpawned() );
		Global.IPlayerEvents.Post( x => x.OnPlayerSpawned( player ) );
	}

	void Global.ISaveEvents.AfterLoad( string filename )
	{
		if ( !Networking.IsHost ) return;

		// Make sure we spawn any players that weren't included in the loaded save
		foreach ( var connection in Connection.All )
		{
			var playerData = CreatePlayerInfo( connection );
			SpawnPlayer( playerData );
		}
	}

	/// <summary>
	/// Called by the client (via PlayerObserver) when they want to respawn.
	/// </summary>
	[Rpc.Host]
	internal void RequestRespawn()
	{
		var connection = Rpc.Caller;

		// Clean up any lingering observers for this connection.
		foreach ( var observer in Scene.GetAllComponents<PlayerObserver>().Where( x => x.Network.Owner == connection ).ToArray() )
		{
			observer.GameObject.Destroy();
		}

		SpawnPlayer( connection );
	}

	/// <summary>Approximate standing-player hull, used for spawn clearance and floor settling.</summary>
	private static readonly BBox PlayerHull = new( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 72f ) );

	/// <summary>The last position we spawned a player at, so we can avoid reusing it.</summary>
	private Vector3? _lastSpawnPosition;

	/// <summary>
	/// Find a place to respawn: a floor-settled spawn point nobody is standing on, avoiding the
	/// last one used.
	/// </summary>
	Transform FindSpawnLocation()
	{
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();

		// No spawn points — fall back to the world origin and warn. The map needs a SpawnPoint.
		if ( spawnPoints.Length == 0 )
		{
			Log.Warning( "No SpawnPoint components in the scene — spawning at the world origin. Add a SpawnPoint to the map to fix this." );
			return Transform.Zero;
		}

		var transform = SpawnPlacement.FindSpawnPosition(
			spawnPoints.OrderBy( _ => Random.Shared.Next() ).Select( x => x.Transform.World ),
			PlayerHull,
			avoid: _lastSpawnPosition );

		_lastSpawnPosition = transform.Position;
		return transform;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void NotifyConsole( string msg )
	{
		Log.Info( msg );
	}

	/// <summary>
	/// Called on the host when a played is killed
	/// </summary>
	internal void OnDeath( Player player, DamageInfo dmg )
	{
		Assert.True( Networking.IsHost );

		Assert.True( player.IsValid(), "Player invalid" );
		Assert.True( player.PlayerData.IsValid(), $"{player.GameObject.Name}'s PlayerData invalid" );

		var source = dmg.Attacker?.GetComponentInParent<IKillSource>( true );
		if ( source == null ) return;

		var isSuicide = source is Player p && p == player;

		if ( !isSuicide )
			source.OnKill( player.GameObject );

		// Fire kill event on the killer if they're a player
		if ( !isSuicide && source is Player killer )
		{
			var killEvent = new PlayerKillEvent { Player = killer, Victim = player.GameObject, DamageInfo = dmg };
			Local.IPlayerEvents.PostToGameObject( killer.GameObject, x => x.OnKill( killEvent ) );
			Global.IPlayerEvents.Post( x => x.OnPlayerKill( killEvent ) );
		}

		player.PlayerData.Deaths++;

		var weapon = dmg.Weapon;
		var w = weapon.IsValid() ? weapon.GetComponentInChildren<IKillIcon>() : null;
		var damageTags = dmg.Tags.ToString() + ( isSuicide ? " suicide" : "" );
		var attackerTags = isSuicide ? "" : source.Tags;
		var attackerName = isSuicide ? null : source.DisplayName;
		var attackerSteamId = isSuicide ? 0L : source.SteamId;
		var playerName = player.Network.Owner?.DisplayName ?? "Unknown";
		Scene.RunEvent<Feed>( x => x.NotifyKill( playerName, attackerName, attackerSteamId, damageTags, attackerTags, "", w?.DisplayIcon ) );

		if ( string.IsNullOrEmpty( attackerName ) )
		{
			NotifyConsole( $"{playerName} died (tags: {dmg.Tags})" );
		}
		else if ( weapon.IsValid() )
		{
			NotifyConsole( $"{attackerName} killed {(isSuicide ? "self" : playerName)} with {weapon.Name} (tags: {dmg.Tags})" );
		}
		else
		{
			NotifyConsole( $"{attackerName} killed {(isSuicide ? "self" : playerName)} (tags: {dmg.Tags})" );
		}
	}

	/// <summary>
	/// Called on the host when an NPC is killed. Credits the attacker and adds a kill feed entry.
	/// </summary>
	internal void OnNpcDeath( Npc victim, DamageInfo dmg )
	{
		Assert.True( Networking.IsHost );
		Assert.True( victim.IsValid(), "NPC invalid" );

		var source = dmg.Attacker?.GetComponent<IKillSource>();
		source?.OnKill( victim.GameObject );

		var w = dmg.Weapon.IsValid() ? dmg.Weapon.GetComponentInChildren<IKillIcon>() : null;
		var attackerName = source?.DisplayName;
		var attackerSteamId = source?.SteamId ?? 0L;
		var attackerTags = source?.Tags ?? "";

		Scene.RunEvent<Feed>( x => x.NotifyKill( victim.DisplayName, attackerName, attackerSteamId, dmg.Tags.ToString(), attackerTags, "npc", w?.DisplayIcon ) );
	}

	/// <summary>
	/// Change a property, remotely
	/// </summary>
	[Rpc.Host]
	internal static void ChangeProperty( Component c, string propertyName, object value )
	{
		if ( !c.IsValid() ) return;
		if ( !c.GameObject.HasAccess( Rpc.Caller ) ) return;

		var tl = TypeLibrary.GetType( c.GetType() );
		if ( tl is null ) return;

		var prop = tl.GetProperty( propertyName );
		if ( prop is null ) return;

		prop.SetValue( c, value );

		// Broadcast the change to everyone

		// BUG - this is optimal I think, but doesn't work??
		// c.GameObject.Network.Refresh( c );

		c.GameObject.Network?.Refresh();
	}

	/// <summary>
	/// Apply a debounced batch of morph changes to a <see cref="SkinnedModelRenderer"/>,
	/// replicated to all clients. Only the morphs present in the batch are modified.
	/// </summary>
	[Rpc.Host]
	internal static void ApplyMorphBatch( SkinnedModelRenderer smr, string morphsJson )
	{
		if ( !smr.IsValid() ) return;
		if ( !smr.GameObject.HasAccess( Rpc.Caller ) ) return;

		smr.GameObject.GetOrAddComponent<MorphState>().ApplyBatch( morphsJson );
	}

	/// <summary>
	/// Apply a full morph preset (as json), and captures with <see cref="MorphState"/> which replicates changes to other clients
	/// </summary>
	[Rpc.Host]
	internal static void ApplyFacePosePreset( SkinnedModelRenderer smr, string morphsJson )
	{
		if ( !smr.IsValid() ) return;
		if ( !smr.GameObject.HasAccess( Rpc.Caller ) ) return;

		smr.GameObject.GetOrAddComponent<MorphState>().ApplyPreset( morphsJson );
	}

	[Rpc.Host]
	internal static async void ChangeMaterialOverride( ModelRenderer renderer, int materialIndex, string materialPath )
	{
		if ( !renderer.IsValid() ) return;
		if ( !renderer.GameObject.HasAccess( Rpc.Caller ) ) return;

		Material material = null;

		if ( !string.IsNullOrEmpty( materialPath ) )
		{
			material = Material.Load( materialPath );
			material ??= await Cloud.Load<Material>( materialPath );
		}

		if ( !renderer.IsValid() ) return;

		renderer.Materials.SetOverride( materialIndex, material );

		renderer.GameObject.Network?.Refresh();
	}

	/// <summary>
	/// Delete an object from the Inspector context menu.
	/// </summary>
	[Rpc.Host]
	internal static void DeleteInspectedObject( GameObject go )
	{
		if ( !go.IsValid() || go.IsProxy ) return;
		if ( go.Tags.Has( "player" ) ) return;

		// Check ownership if the object has an Ownable component
		if ( !go.HasAccess( Rpc.Caller ) ) return;

		go.Destroy();
	}

	/// <summary>
	/// Break (gib) a prop from the Inspector context menu.
	/// </summary>
	[Rpc.Host]
	internal static void BreakInspectedProp( Prop prop )
	{
		if ( !prop.IsValid() || prop.IsProxy ) return;
		// Check ownership if the object has an Ownable component
		if ( !prop.GameObject.HasAccess( Rpc.Caller ) ) return;

		var damageable = prop.GetComponent<Component.IDamageable>();
		if ( damageable is null ) return;

		var dmg = new DamageInfo( 999999, null, null );
		dmg.Tags.Add( DamageTags.GibAlways );
		damageable.OnDamage( in dmg );
	}

	[Rpc.Host]
	internal static void GiveSpawnerWeaponAt( string type, string path, int slot, string data = null, string icon = null, string title = null )
	{
		var player = Player.FindForConnection( Rpc.Caller );
		if ( player is null ) return;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		if ( slot < 0 || slot >= inventory.MaxSlots ) return;

		ISpawner s = type switch
		{
			"prop" or "mount" => new PropSpawner( path ),
			"entity" or "sent" => new EntitySpawner( path ),
			"dupe" when data is not null => DuplicatorSpawner.FromJson( data, title, icon ),
			_ => null
		};

		if ( s is null ) return;

		var loadout = player.GetComponent<PlayerLoadout>();

		// If there's already a spawner weapon in this slot, just update
		if ( inventory.GetSlot( slot ) is SpawnerWeapon existingSpawner )
		{
			existingSpawner.SetSpawner( s );
			inventory.SwitchWeapon( existingSpawner );
			loadout?.SaveLoadout();
			return;
		}

		// Slot is occupied by something else — don't replace it
		if ( inventory.GetSlot( slot ).IsValid() ) return;

		inventory.Pickup( "weapons/spawner/spawner.prefab", slot, false );
		var spawner = inventory.GetSlot( slot ) as SpawnerWeapon;
		if ( !spawner.IsValid() ) return;

		spawner.SetSpawner( s );
		inventory.SwitchWeapon( spawner );
		loadout?.SaveLoadout();
	}

	void IScenePhysicsEvents.OnOutOfBounds( Rigidbody body )
	{
		body.DestroyGameObject();
	}

	void ICleanupEvents.OnCleanup( int removedObjects, int restoredObjects )
	{
		var message = $"Cleanup! Removed {removedObjects} objects.";
		if ( restoredObjects > 0 )
			message += $" Restored {restoredObjects} objects.";

		Notices.AddNotice( "cleaning_services", Color.Green, message );
	}
}
