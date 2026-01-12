public sealed partial class GameManager : GameObjectSystem<GameManager>, Component.INetworkListener, ISceneStartup
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
	/// In the editor, spawn the player where they're looking
	/// </summary>
	public static Transform EditorSpawnLocation { get; set; }

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
			if ( Application.IsEditor && !EditorSpawnLocation.Position.IsNearlyZero() )
			{
				return EditorSpawnLocation;
			}

			return Transform.Zero;
		}

		var players = Scene.GetAll<Player>();

		if ( !players.Any() )
		{
			return Random.Shared.FromArray( spawnPoints ).Transform.World;
		}

		//
		// Find spawnpoint furthest away from any players
		// TODO: in the future we may want a different logic, as spawning far away is not necessarily good.
		// But good enough for now and also reduces chances of players from spawning on top of  or inside each other.
		//
		SpawnPoint spawnPointFurthestAway = null;
		float spawnPointFurthestAwayDistanceSqr = float.MinValue;

		foreach ( var spawnPoint in spawnPoints )
		{
			float closestPlayerDistanceToSpawnpointSqr = float.MaxValue;

			foreach ( var player in players )
			{
				float playerDistanceToSpawnPointSqr = (spawnPoint.Transform.World.Position - player.Transform.World.Position).LengthSquared;

				if ( playerDistanceToSpawnPointSqr < closestPlayerDistanceToSpawnpointSqr )
				{
					closestPlayerDistanceToSpawnpointSqr = playerDistanceToSpawnPointSqr;
				}
			}

			if ( closestPlayerDistanceToSpawnpointSqr > spawnPointFurthestAwayDistanceSqr )
			{
				spawnPointFurthestAwayDistanceSqr = closestPlayerDistanceToSpawnpointSqr;
				spawnPointFurthestAway = spawnPoint;
			}
		}

		return spawnPointFurthestAway.WorldTransform;
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

		using var spawnInfo = new SpawnConfig();
		spawnInfo.Location = new Transform( trace.EndPosition, facingAngle );
		spawnInfo.Path = path_or_ident;

		// TODO - can this user spawn this package?

		// we're a model
		if ( await FindModelPath( spawnInfo ) is Model model )
		{
			SpawnModel( model, spawnTransform, player );
			return;
		}

		// we're a model
		if ( await FindEntityPath( spawnInfo ) is ScriptedEntity entity )
		{
			SpawnEntity( entity, spawnTransform, player );
			return;
		}

		Log.Warning( $"Couldn't resolve '{path_or_ident}'" );

	}

	class SpawnConfig : IDisposable
	{
		public SpawningProgress Placeholder;
		public Transform Location;
		public string Path;

		public void Dispose()
		{
			Placeholder?.GameObject?.Destroy();
		}

		public void CreatePlaceholder()
		{
			if ( Placeholder is not null )
				return;

			const string placeholderPath = "/prefabs/engine/spawn-progress.prefab";

			var go = GameObject.Clone( placeholderPath );
			go.WorldTransform = Location.WithScale( 1 );

			go.NetworkSpawn( true, null );
			Placeholder = go.GetComponent<SpawningProgress>();
		}

		internal void UpdatePlaceholder( Package package )
		{
			var mins = package.GetMeta<Vector3>( "RenderMins", -1 );
			var maxs = package.GetMeta<Vector3>( "RenderMaxs", -1 );

			Placeholder.SpawnBounds = new BBox( mins, maxs );
		}
	}

	static async Task<Model> FindModelPath( SpawnConfig spawn )
	{
		if ( spawn.Path.EndsWith( ".vmdl" ) )
		{
			var se = await ResourceLibrary.LoadAsync<Model>( spawn.Path );
			if ( se is not null ) return se;
		}

		Package package = default;

		// Already downloaded, cool
		if ( Package.TryGetCached( spawn.Path, out package, false ) )
		{
			return await Cloud.Load<Model>( spawn.Path );
		}

		spawn.CreatePlaceholder();

		package = await Package.FetchAsync( spawn.Path, false );
		if ( package is null || package.TypeName != "model" )
			return null;

		spawn.UpdatePlaceholder( package );

		return await Cloud.Load<Model>( spawn.Path );
	}

	static async Task<ScriptedEntity> FindEntityPath( SpawnConfig spawn )
	{
		var se = await ResourceLibrary.LoadAsync<ScriptedEntity>( spawn.Path );
		if ( se is not null ) return se;

		Package package = default;

		// Already downloaded, cool
		if ( Package.TryGetCached( spawn.Path, out package, false ) )
		{
			return await Cloud.Load<ScriptedEntity>( spawn.Path, true );
		}

		spawn.CreatePlaceholder();

		package = await Package.FetchAsync( spawn.Path, false );
		if ( package is null || package.TypeName != "sent" )
			return null;

		spawn.UpdatePlaceholder( package );

		return await Cloud.Load<ScriptedEntity>( spawn.Path, true );
	}


	private static void SpawnModel( Model model, Transform spawnTransform, Player player )
	{
		Log.Info( $"[{player}] Spawning Model {model.Name}" );

		var depth = -model.Bounds.Mins.z;

		spawnTransform.Position += spawnTransform.Up * depth;

		var go = new GameObject( false, "prop" );
		go.Tags.Add( "removable" );
		go.WorldTransform = spawnTransform;

		var prop = go.AddComponent<Prop>();
		prop.Model = model;

		if ( (model.Physics?.Parts?.Count ?? 0) == 0 )
		{
			Log.Info( "No physics - adding a cube" );

			var collider = go.AddComponent<BoxCollider>();
			collider.Scale = model.Bounds.Size;
			collider.Center = model.Bounds.Center;


			go.AddComponent<Rigidbody>();
		}

		go.NetworkSpawn( true, null );

		var undo = player.Undo.Create();
		undo.Name = "Spawn Model";
		undo.Add( go );

	}

	private static void SpawnEntity( ScriptedEntity entity, Transform spawnTransform, Player player )
	{
		Log.Info( $"[{player}] Spawning Entity {entity.Title}" );

		var prefabFile = entity.Prefab;
		//var bounds = prefabFile.GetScene().GetLocalBounds();
		var bounds = SceneUtility.GetPrefabScene( prefabFile ).GetLocalBounds();

		var depth = -bounds.Mins.z;
		spawnTransform.Position += spawnTransform.Up * depth;

		var go = GameObject.Clone( prefabFile, new CloneConfig { Transform = spawnTransform, StartEnabled = false } );
		go.Tags.Add( "removable" );
		go.WorldTransform = spawnTransform;

		go.NetworkSpawn( true, null );

		var undo = player.Undo.Create();
		undo.Name = $"Spawn {entity.Title}";
		undo.Add( go );

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
}
