[Alias( "dynamite" )]
public class DynamiteEntity : Component, IPlayerControllable, Component.IDamageable
{
	[Property, Range( 1, 500 ), Step( 1 ), ClientEditable]
	public float Damage { get; set; } = 128;

	[Property, Range( 16, 4096 ), Step( 16 ), ClientEditable]
	public float Radius { get; set; } = 1024f;

	[Property, Range( 1, 100 ), Step( 1 ), ClientEditable]
	public float Force { get; set; } = 1;

	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	bool _isDead = false;

	[Rpc.Host]
	public void Explode()
	{
		_isDead = true;

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
		}, FindMode.EverythingInSelfAndDescendants );

		go.Enabled = true;
		go.NetworkSpawn( true, null );

		GameObject.Destroy();
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( _isDead ) return;
		if ( IsProxy ) return;

		Explode();
	}

	void IPlayerControllable.OnControl()
	{
		if ( Activate.Pressed() )
		{
			Explode();
		}
	}

	void IPlayerControllable.OnEndControl()
	{
		// nothing to do
	}

	void IPlayerControllable.OnStartControl()
	{
		// nothing to do
	}
}
