public sealed partial class GameManager : GameObjectSystem<GameManager>, Component.INetworkListener, ISceneStartup, IScenePhysicsEvents
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
	}

	private PlayerData CreatePlayerInfo( Connection channel )
	{
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

	[Rpc.Broadcast]
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

		var weapon = dmg.Weapon;
		var attacker = dmg.Attacker?.GetComponent<Player>();

		if ( !dmg.Attacker.IsValid() || !attacker.IsValid() )
		{
			return;
		}

		var isSuicide = attacker == player;

		if ( attacker.IsValid() && !isSuicide )
		{
			Assert.True( weapon.IsValid(), $"Weapon invalid. (Attacker: {attacker.DisplayName}, Victim: {player.DisplayName})" );

			attacker.PlayerData.Kills++;
			attacker.PlayerData.AddStat( $"kills" );

			if ( weapon.IsValid() )
			{
				attacker.PlayerData.AddStat( $"kills.{weapon.Name}" );
			}
		}

		player.PlayerData.Deaths++;

		var w = weapon.IsValid() ? weapon.GetComponentInChildren<IKillIcon>() : null;
		Scene.RunEvent<Feed>( x => x.NotifyDeath( player.PlayerData, attacker.PlayerData, w?.DisplayIcon, dmg.Tags ) );

		var attackerName = attacker.IsValid() ? attacker.DisplayName : dmg.Attacker?.Name;
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

	[ConCmd( "spawn" )]
	private static void SpawnCommand( string path_or_ident )
	{
		Spawn( path_or_ident );
	}

	[Rpc.Broadcast]
	public static async void Spawn( string path_or_ident )
	{
		// if we're the person calling this, then we don't do anything but add the spawn stat
		if ( Rpc.Caller == Connection.Local )
		{
			var data = new Dictionary<string, object>();
			data["ident"] = path_or_ident;
			Sandbox.Services.Stats.Increment( "spawn", 1, data );

			Sound.Play( "sounds/ui/ui.spawn.sound" );
		}

		// Only actually spawn it on the host
		if ( !Networking.IsHost )
			return;

		var player = Player.FindForConnection( Rpc.Caller );
		if ( player is null ) return;

		// store off their eye transform
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

		// Try as a model
		if ( await FindModel( path_or_ident ) is not null )
		{
			var spawner = new PropSpawner( path_or_ident );
			if ( await spawner.Loading )
			{
				await SpawnAndUndo( spawner, spawnTransform, player );
				return;
			}
		}

		// Try as an entity
		if ( await FindEntity( path_or_ident ) is not null )
		{
			var spawner = new EntitySpawner( path_or_ident );
			if ( await spawner.Loading )
			{
				await SpawnAndUndo( spawner, spawnTransform, player );
				return;
			}
		}

		Log.Warning( $"Couldn't resolve '{path_or_ident}'" );
	}

	/// <summary>
	/// Spawn using any <see cref="ISpawner"/> and register undo.
	/// </summary>
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
	/// Try to resolve a path as a model. Returns null if it's not a model.
	/// </summary>
	static async Task<Model> FindModel( string path )
	{
		if ( path.EndsWith( ".vmdl" ) )
			return await ResourceLibrary.LoadAsync<Model>( path );

		if ( Package.TryGetCached( path, out var package, false ) )
			return await Cloud.Load<Model>( path );

		package = await Package.FetchAsync( path, false );
		if ( package is null || package.TypeName != "model" )
			return null;

		return await Cloud.Load<Model>( path );
	}

	/// <summary>
	/// Try to resolve a path as a scripted entity. Returns null if it's not an entity.
	/// </summary>
	static async Task<ScriptedEntity> FindEntity( string path )
	{
		var se = await ResourceLibrary.LoadAsync<ScriptedEntity>( path );
		if ( se is not null ) return se;

		if ( Package.TryGetCached( path, out var package, false ) )
			return await Cloud.Load<ScriptedEntity>( path, true );

		package = await Package.FetchAsync( path, false );
		if ( package is null || package.TypeName != "sent" )
			return null;

		return await Cloud.Load<ScriptedEntity>( path, true );
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
			"prop" => new PropSpawner( path ),
			"entity" => new EntitySpawner( path ),
			"dupe" when data is not null => DuplicatorSpawner.FromJson( data, title, icon ),
			_ => null
		};

		if ( s is null ) return;

		// If there's already a spawner weapon in this slot, just update
		if ( inventory.GetSlot( slot ) is SpawnerWeapon existingSpawner )
		{
			existingSpawner.SetSpawner( s );
			inventory.SwitchWeapon( existingSpawner );
			return;
		}

		// Slot is occupied by something else — don't replace it
		if ( inventory.GetSlot( slot ).IsValid() ) return;

		Log.Info( $"What" );

		inventory.Pickup( "weapons/spawner/spawner.prefab", slot, false );
		var spawner = inventory.GetSlot( slot ) as SpawnerWeapon;
		if ( !spawner.IsValid() ) return;

		spawner.SetSpawner( s );
		inventory.SwitchWeapon( spawner );
	}

	void IScenePhysicsEvents.OnOutOfBounds( Rigidbody body )
	{
		body.DestroyGameObject();
	}
}
