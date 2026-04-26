public sealed partial class GameManager 
{
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
			await SpawnAndUndo( spawner, spawnTransform, player );
			return;
		}

		Log.Warning( $"Couldn't resolve '{ident}' — spawner={(spawner is null ? "null" : "not ready")}" );
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
		var spawnData = new ISpawnEvents.SpawnData
		{
			Spawner = spawner,
			Transform = transform,
			Player = player?.PlayerData
		};

		Game.ActiveScene.RunEvent<ISpawnEvents>( x => x.OnSpawn( spawnData ) );

		if ( spawnData.Cancelled )
			return;

		var objects = await spawner.Spawn( transform, player );

		if ( objects is { Count: > 0 } )
		{
			var undo = player.Undo.Create();
			undo.Name = $"Spawn {spawner.DisplayName}";

			foreach ( var go in objects )
			{
				undo.Add( go );
			}

			Game.ActiveScene.RunEvent<ISpawnEvents>( x => x.OnPostSpawn( new ISpawnEvents.PostSpawnData
			{
				Spawner = spawner,
				Transform = transform,
				Player = player?.PlayerData,
				Objects = objects
			} ) );
		}
	}
}
