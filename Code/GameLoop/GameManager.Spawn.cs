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
	public static async void Spawn( string ident, string metadata = null, bool forceWorld = false )
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

		var trace = Game.SceneTrace.Ray( eyes.Position, eyes.Position + eyes.Forward * 2048 )
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

		var spawner = ISpawner.Create( type, path, source, metadata );

		if ( spawner is not null && await spawner.Loading )
		{
			await SpawnAndUndo( spawner, spawnTransform, player, forceWorld );
			return;
		}

		Log.Warning( $"Couldn't resolve '{ident}'" );
	}

	private static async Task SpawnAndUndo( ISpawner spawner, Transform transform, Player player, bool forceWorld = false )
	{
		var spawnData = new Global.ISpawnEvents.SpawnData
		{
			Spawner = spawner,
			Transform = transform,
			Player = player?.PlayerData
		};

		Game.ActiveScene.RunEvent<Global.ISpawnEvents>( x => x.OnSpawn( spawnData ) );

		if ( spawnData.Cancelled )
			return;

		// If the prefab is a weapon, pick it up directly instead of spawning into the world
		if ( !forceWorld )
		{
			var prefab = spawner.Prefab;
			if ( prefab is not null && prefab.GetComponent<BaseCarryable>( true ) is not null )
			{
				var inventory = player.GetComponent<PlayerInventory>();
				inventory.Pickup( prefab );
				return;
			}
		}

		var objects = await spawner.Spawn( transform, player );

		if ( objects is { Count: > 0 } )
		{
			var undo = player.Undo.Create();
			undo.Name = $"Spawn {spawner.DisplayName}";

			foreach ( var go in objects )
			{
				undo.Add( go );
			}

			Game.ActiveScene.RunEvent<Global.ISpawnEvents>( x => x.OnPostSpawn( new Global.ISpawnEvents.PostSpawnData
			{
				Spawner = spawner,
				Transform = transform,
				Player = player?.PlayerData,
				Objects = objects
			} ) );
		}
	}
}
