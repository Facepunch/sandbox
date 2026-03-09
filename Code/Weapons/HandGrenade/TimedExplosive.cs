using Sandbox;

/// <summary>
/// Explodes after a set time. Spawns an explosion prefab with configurable radius, damage, and force.
/// </summary>
public sealed class TimedExplosive : Component
{
	[Property] public float Lifetime { get; set; } = 3f;
	[Property] public float Radius { get; set; } = 256f;
	[Property] public float Damage { get; set; } = 125f;
	[Property] public float Force { get; set; } = 1f;

	TimeSince TimeSinceCreated { get; set; }

	protected override void OnEnabled()
	{
		TimeSinceCreated = 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( TimeSinceCreated < Lifetime ) return;

		Explode();
	}

	[Rpc.Host]
	public void Explode()
	{
		var explosionPrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/explosion_med.prefab" );
		if ( explosionPrefab == null )
		{
			Log.Warning( "Can't find /prefabs/engine/explosion_med.prefab" );
			return;
		}

		var go = GameObject.Clone( explosionPrefab, new CloneConfig { Transform = WorldTransform.WithScale( 1 ), StartEnabled = false } );
		if ( !go.IsValid() ) return;

		go.RunEvent<RadiusDamage>( x =>
		{
			x.Radius = Radius;
			x.PhysicsForceScale = Force;
			x.DamageAmount = Damage;
			x.Attacker = go;
		}, FindMode.EverythingInSelfAndDescendants );

		go.Enabled = true;
		go.NetworkSpawn( true, null );

		GameObject.Destroy();
	}
}
