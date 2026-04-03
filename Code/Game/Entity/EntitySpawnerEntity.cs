using Sandbox.UI;

/// <summary>
/// A world-placed SENT that spawns another SENT at its location.
/// Can be triggered manually via player input or automatically on a timer.
/// </summary>
[Alias( "entity_spawner" )]
public class EntitySpawnerEntity : Component, IPlayerControllable
{
	/// <summary>
	/// The SENT to spawn.
	/// </summary>
	[Property, ClientEditable]
	public ScriptedEntity Entity { get; set; }

	/// <summary>
	/// Input binding that triggers a manual spawn when the player uses this entity.
	/// </summary>
	[Property, Sync, ClientEditable, Group( "Input" )]
	public ClientInput SpawnInput { get; set; }

	/// <summary>
	/// When enabled, spawns the entity automatically every <see cref="SpawnInterval"/> seconds.
	/// </summary>
	[Property, ClientEditable, Group( "Auto Spawn" )]
	public bool AutoSpawn { get; set; } = false;

	/// <summary>
	/// Seconds between automatic spawns.
	/// </summary>
	[Property, ClientEditable, Range( 0.5f, 300f, 0.5f ), Group( "Auto Spawn" )]
	public float SpawnInterval { get; set; } = 5f;

	/// <summary>
	/// Maximum number of entities spawned by this spawner allowed to exist at once.
	/// New spawns are suppressed until existing ones are destroyed.
	/// </summary>
	[Property, ClientEditable, Range( 1, 50, 1 ), Group( "Auto Spawn" )]
	public int MaxEntities { get; set; } = 5;

	private TimeSince _timeSinceLastSpawn;
	private readonly List<WeakReference<GameObject>> _spawnedEntities = new();

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( !AutoSpawn ) return;
		if ( _timeSinceLastSpawn < SpawnInterval ) return;

		DoSpawn();
	}

	void IPlayerControllable.OnControl()
	{
		if ( SpawnInput.Pressed() )
			DoSpawn();
	}

	void IPlayerControllable.OnStartControl() { }
	void IPlayerControllable.OnEndControl() { }

	[Rpc.Host]
	private void DoSpawn()
	{
		if ( !Entity.IsValid() || Entity.Prefab is null ) return;

		// Prune destroyed entities from the tracking list
		_spawnedEntities.RemoveAll( wr => !wr.TryGetTarget( out var go ) || !go.IsValid() );

		if ( _spawnedEntities.Count >= MaxEntities ) return;

		_timeSinceLastSpawn = 0;

		var spawned = GameObject.Clone( Entity.Prefab, new CloneConfig
		{
			Transform = WorldTransform,
			StartEnabled = false,
		} );

		spawned.Tags.Add( "removable" );
		spawned.NetworkSpawn( true, null );
		spawned.Enabled = true;

		_spawnedEntities.Add( new WeakReference<GameObject>( spawned ) );
	}
}
