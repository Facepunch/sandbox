using Sandbox;

/// <summary>
/// Explodes after a set time. Spawns an explosion prefab with configurable radius, damage, and force.
/// </summary>
public sealed class TimedExplosive : Component, Component.IDamageable
{
	[Property] public float Lifetime { get; set; } = 3f;
	[Property] public float Radius { get; set; } = 256f;
	[Property] public float Damage { get; set; } = 125f;
	[Property] public float Force { get; set; } = 1f;

	/// <summary>Who gets credit/blame for the explosion (the thrower). Host-side only. Falls back to the explosion itself.</summary>
	public GameObject Attacker { get; set; }

	TimeSince TimeSinceCreated { get; set; }

	bool HasExploded { get; set; }

	protected override void OnEnabled()
	{
		TimeSinceCreated = 0;
		HasExploded = false;
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
		if ( HasExploded ) return;

		var explosionPrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/explosion_med.prefab" );
		if ( explosionPrefab == null )
		{
			Log.Warning( "Can't find /prefabs/engine/explosion_med.prefab" );
			return;
		}

		HasExploded = true;

		var go = GameObject.Clone( explosionPrefab, new CloneConfig { Transform = WorldTransform.WithScale( 1 ), StartEnabled = false } );
		if ( !go.IsValid() )
		{
			HasExploded = false;
			return;
		}

		go.RunEvent<RadiusDamage>( x =>
		{
			x.Radius = Radius;
			x.PhysicsForceScale = Force;
			x.DamageAmount = Damage;
			x.Attacker = Attacker.IsValid() ? Attacker : go;
		}, FindMode.EverythingInSelfAndDescendants );

		go.Enabled = true;
		go.NetworkSpawn( true, null );

		GameObject.Destroy();
	}

	void Component.IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy || !Networking.IsHost || HasExploded || damage.Damage <= 0f ) return;
		if ( damage.Tags.Contains( DamageTags.Crush ) ) return;
		if ( damage.Tags.Contains( DamageTags.Impact ) ) return;

		Explode();
	}

}
