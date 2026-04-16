using Sandbox.UI;

public sealed partial class GameManager : GameObjectSystem<GameManager>, Component.INetworkListener, ISceneStartup, IScenePhysicsEvents, ICleanupEvents, ISaveEvents
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

	void Component.INetworkListener.OnActive( Connection channel )
	{
		channel.CanSpawnObjects = false;

		var playerData = CreatePlayerInfo( channel );
		SpawnPlayer( playerData );
		CheckConnectionAchievement( channel );
		CheckFriendsOnlineStat();

		Scene.Get<Chat>()?.AddSystemText( $"{channel.DisplayName} has joined the game", "👋" );
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

		if ( _kickedPlayers.Remove( channel.Id ) ) return;
		if ( BanSystem.Current?.IsBanned( channel.SteamId ) ?? false ) return;

		Scene.Get<Chat>()?.AddSystemText( $"{channel.DisplayName} has left the game", "👋" );
	}

	private PlayerData CreatePlayerInfo( Connection channel )
	{
		var existingPlayerInfo = PlayerData.For( channel );
		if ( existingPlayerInfo.IsValid() )
			return existingPlayerInfo;

		var go = new GameObject( true, $"PlayerInfo - {channel.DisplayName}" );
		var data = go.AddComponent<PlayerData>();
		data.SteamId = (long)channel.SteamId;
		data.PlayerId = channel.Id;
		data.DisplayName = channel.DisplayName;

		go.NetworkSpawn( null );
		go.Network.SetOwnerTransfer( OwnerTransfer.Fixed );

		return data;
	}

	public void SpawnPlayer( Connection connection ) => SpawnPlayer( PlayerData.For( connection ) );

	public void SpawnPlayer( PlayerData playerData )
	{
		Assert.NotNull( playerData, "PlayerData is null" );
		Assert.True( Networking.IsHost, $"Client tried to SpawnPlayer: {playerData.DisplayName}" );

		// does this connection already have a player?
		if ( Scene.GetAll<Player>().Any( x => x.Network.Owner?.Id == playerData.PlayerId ) )
			return;

		// Find a spawn location for this player
		var startLocation = FindSpawnLocation().WithScale( 1 );

		// Spawn this object and make the client the owner
		var playerGo = GameObject.Clone( "/prefabs/engine/player.prefab", new CloneConfig { Name = playerData.DisplayName, StartEnabled = false, Transform = startLocation } );

		var player = playerGo.Components.Get<Player>( true );
		player.PlayerData = playerData;

		var owner = Connection.Find( playerData.PlayerId );
		playerGo.NetworkSpawn( owner );

		IPlayerEvent.PostToGameObject( player.GameObject, x => x.OnSpawned() );
		player.EquipBestWeapon();
	}

	void ISaveEvents.AfterLoad( string filename )
	{
		if ( !Networking.IsHost ) return;

		// Make sure we spawn any players that weren't included in the loaded save
		foreach ( var connection in Connection.All )
		{
			var playerData = CreatePlayerInfo( connection );
			SpawnPlayer( playerData );
		}
	}

	public void SpawnPlayerDelayed( PlayerData playerData )
	{
		GameTask.RunInThreadAsync( async () =>
		{
			await Task.Delay( 4000 );
			await GameTask.MainThread();
			if ( Current is not null )
				Current.SpawnPlayer( playerData );
		} );
	}

	/// <summary>
	/// Find the most appropriate place to respawn
	/// </summary>
	Transform FindSpawnLocation()
	{
		//
		// If we have any SpawnPoint components in the scene, then use those
		//
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();

		if ( spawnPoints.Length == 0 )
		{
			return Transform.Zero;
		}

		return Random.Shared.FromArray( spawnPoints ).Transform.World;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void SendMessage( string msg )
	{
		Log.Info( msg );
	}

	/// <summary>
	/// Called on the host when a played is killed
	/// </summary>
	public void OnDeath( Player player, DamageInfo dmg )
	{
		Assert.True( Networking.IsHost );

		Assert.True( player.IsValid(), "Player invalid" );
		Assert.True( player.PlayerData.IsValid(), $"{player.GameObject.Name}'s PlayerData invalid" );

		var source = dmg.Attacker?.GetComponentInParent<IKillSource>( true );
		if ( source == null ) return;

		var isSuicide = source is Player p && p == player;

		if ( !isSuicide )
			source.OnKill( player.GameObject );

		player.PlayerData.Deaths++;

		var weapon = dmg.Weapon;
		var w = weapon.IsValid() ? weapon.GetComponentInChildren<IKillIcon>() : null;
		var damageTags = dmg.Tags.ToString() + ( isSuicide ? " suicide" : "" );
		var attackerTags = isSuicide ? "" : source.Tags;
		var attackerName = isSuicide ? null : source.DisplayName;
		var attackerSteamId = isSuicide ? 0L : source.SteamId;
		Scene.RunEvent<Feed>( x => x.NotifyKill( player.DisplayName, attackerName, attackerSteamId, damageTags, attackerTags, "", w?.DisplayIcon ) );

		if ( string.IsNullOrEmpty( attackerName ) )
		{
			SendMessage( $"{player.DisplayName} died (tags: {dmg.Tags})" );
		}
		else if ( weapon.IsValid() )
		{
			SendMessage( $"{attackerName} killed {(isSuicide ? "self" : player.DisplayName)} with {weapon.Name} (tags: {dmg.Tags})" );
		}
		else
		{
			SendMessage( $"{attackerName} killed {(isSuicide ? "self" : player.DisplayName)} (tags: {dmg.Tags})" );
		}
	}

	/// <summary>
	/// Called on the host when an NPC is killed. Credits the attacker and adds a kill feed entry.
	/// </summary>
	public void OnNpcDeath( string npcName, DamageInfo dmg )
	{
		Assert.True( Networking.IsHost );

		var source = dmg.Attacker?.GetComponent<IKillSource>();
		source?.OnKill( dmg.Attacker );

		var w = dmg.Weapon.IsValid() ? dmg.Weapon.GetComponentInChildren<IKillIcon>() : null;
		var attackerName = source?.DisplayName;
		var attackerSteamId = source?.SteamId ?? 0L;
		var attackerTags = source?.Tags ?? "";

		Scene.RunEvent<Feed>( x => x.NotifyKill( npcName, attackerName, attackerSteamId, dmg.Tags.ToString(), attackerTags, "npc", w?.DisplayIcon ) );
	}

	[ConCmd( "spawn" )]
	private static void SpawnCommand( string ident )
	{
		Spawn( ident );
	}

	/// <summary>
	/// Spawn from a string identifier (e.g. "prop:path", "entity:path", "dupe.local:id", "dupe.workshop:id").
	/// Optional metadata string is passed through to the spawner for type-specific use (e.g. mount bounds/title).
	/// </summary>
	[Rpc.Broadcast]
	public static async void Spawn( string ident, string metadata = null )
	{
		// if we're the person calling this, then we don't do anything but add the spawn stat
		if ( Rpc.Caller == Connection.Local )
		{
			var data = new Dictionary<string, object>();
			data["ident"] = ident;
			Sandbox.Services.Stats.Increment( "spawn", 1, data );

			Sound.Play( "sounds/ui/ui.spawn.sound" );
		}

		// Only actually spawn it on the host
		if ( !Networking.IsHost )
			return;

		var player = Player.FindForConnection( Rpc.Caller );
		if ( player is null ) return;

		var eyes = player.EyeTransform;

		var trace = Game.SceneTrace.Ray( eyes.Position, eyes.Position + eyes.Forward * 200 )
			.IgnoreGameObject( player.GameObject )
			.WithoutTags( "player" )
			.Run();

		var up = trace.Normal;
		var backward = -eyes.Forward;

		var right = Vector3.Cross( up, backward ).Normal;
		var forward = Vector3.Cross( right, up ).Normal;
		var facingAngle = Rotation.LookAt( forward, up );

		var spawnTransform = new Transform( trace.EndPosition, facingAngle );

		// TODO - can this user spawn this package?

		var (type, path, source) = SpawnlistItem.ParseIdent( ident );

		ISpawner spawner = type switch
		{
			"prop" => new PropSpawner( path ),
			"mount" => new MountSpawner( path, metadata ),
			"entity" or "sent" => new EntitySpawner( path ),
			"dupe" => await FindDupe( path, source ),
			_ => null
		};

		if ( spawner is not null && await spawner.Loading )
		{
			Log.Info( $"[Spawn] Spawning '{ident}' type='{type}' spawner={spawner.GetType().Name} metadata={(metadata ?? "null")}" );
			await SpawnAndUndo( spawner, spawnTransform, player );
			return;
		}

		Log.Warning( $"[Spawn] Couldn't resolve '{ident}' — spawner={(spawner is null ? "null" : "not ready")}" );
	}

	/// <summary>
	/// Resolve a dupe ident to a <see cref="DuplicatorSpawner"/>, this sucks a bit but okay, the DuplicatorSpawner should handle this
	/// </summary>
	private static async Task<DuplicatorSpawner> FindDupe( string id, string source )
	{
		if ( !ulong.TryParse( id, out var fileId ) )
			return null;

		if ( source == "workshop" )
		{
			var query = new Storage.Query { FileIds = [fileId] };

			var result = await query.Run();
			var item = result.Items?.FirstOrDefault();
			if ( item is null ) return null;

			var installed = await item.Install();
			if ( installed is null ) return null;

			var json = await installed.Files.ReadAllTextAsync( "/dupe.json" );
			return DuplicatorSpawner.FromJson( json, item.Title );
		}

		var entry = Storage.GetAll( "dupe" ).FirstOrDefault( x => x.Id.ToString() == fileId.ToString() );
		if ( entry is null ) return null;

		var dupeJson = await entry.Files.ReadAllTextAsync( "/dupe.json" );
		return DuplicatorSpawner.FromJson( dupeJson, entry.GetMeta<string>( "name" ) );
	}

	private static async Task SpawnAndUndo( ISpawner spawner, Transform transform, Player player )
	{
		var objects = await spawner.Spawn( transform, player );

		if ( objects is { Count: > 0 } )
		{
			var undo = player.Undo.Create();
			undo.Name = $"Spawn {spawner.DisplayName}";

			foreach ( var go in objects )
			{
				undo.Add( go );
			}
		}
	}

	/// <summary>
	/// Change a property, remotely
	/// </summary>
	[Rpc.Host]
	public static void ChangeProperty( Component c, string propertyName, object value )
	{
		if ( !c.IsValid() ) return;

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
	public static void ApplyMorphBatch( SkinnedModelRenderer smr, string morphsJson )
	{
		if ( !smr.IsValid() ) return;
		smr.GameObject.GetOrAddComponent<MorphState>().ApplyBatch( morphsJson );
	}

	/// <summary>
	/// Apply a full morph preset (as json), and captures with <see cref="MorphState"/> which replicates changes to other clients
	/// </summary>
	[Rpc.Host]
	public static void ApplyFacePosePreset( SkinnedModelRenderer smr, string morphsJson )
	{
		if ( !smr.IsValid() ) return;
		smr.GameObject.GetOrAddComponent<MorphState>().ApplyPreset( morphsJson );
	}

	[Rpc.Host]
	public static async void ChangeMaterialOverride( ModelRenderer renderer, int materialIndex, string materialPath )
	{
		if ( !renderer.IsValid() ) return;

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
	public static void DeleteInspectedObject( GameObject go )
	{
		if ( !go.IsValid() || go.IsProxy ) return;
		if ( go.Tags.Has( "player" ) ) return;

		go.Destroy();
	}

	/// <summary>
	/// Break (gib) a prop from the Inspector context menu.
	/// </summary>
	[Rpc.Host]
	public static void BreakInspectedProp( Prop prop )
	{
		if ( !prop.IsValid() || prop.IsProxy ) return;

		var damageable = prop.GetComponent<Component.IDamageable>();
		if ( damageable is null ) return;

		var dmg = new DamageInfo( 999999, null, null );
		dmg.Tags.Add( DamageTags.GibAlways );
		damageable.OnDamage( in dmg );
	}

	[Rpc.Host]
	public static void GiveSpawnerWeaponAt( string type, string path, int slot, string data = null, string icon = null, string title = null )
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

		// If there's already a spawner weapon in this slot, just update
		if ( inventory.GetSlot( slot ) is SpawnerWeapon existingSpawner )
		{
			existingSpawner.SetSpawner( s );
			inventory.SwitchWeapon( existingSpawner );
			inventory.SaveLoadout();
			return;
		}

		// Slot is occupied by something else — don't replace it
		if ( inventory.GetSlot( slot ).IsValid() ) return;

		inventory.Pickup( "weapons/spawner/spawner.prefab", slot, false );
		var spawner = inventory.GetSlot( slot ) as SpawnerWeapon;
		if ( !spawner.IsValid() ) return;

		spawner.SetSpawner( s );
		inventory.SwitchWeapon( spawner );
		inventory.SaveLoadout();
	}

	void IScenePhysicsEvents.OnOutOfBounds( Rigidbody body )
	{
		body.DestroyGameObject();
	}

	public void OnCleanup( int removedObjects, int restoredObjects )
	{
		Notices.AddNotice( "cleaning_services", Color.Green, $"Cleanup! Removed {removedObjects} objects, restored {restoredObjects} objects." );
	}
}
