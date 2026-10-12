/// <summary>
/// A projectile. It explodes when it hits something.
/// </summary>
public class ProjectileEntity : Component, Component.ICollisionListener
{
	[RequireComponent] public Rigidbody Rigidbody { get; set; }

	[Sync( SyncFlags.FromHost )] public Player Instigator { get; set; }

	protected TimeSince TimeSinceCreated;

	protected override void OnStart()
	{
		Tags.Add( "projectile" );
	}

	protected override void OnEnabled()
	{
		TimeSinceCreated = 0;
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy )
			return;

		var player = collision.Other.GameObject.GetComponentInParent<Player>();
		if ( Instigator.IsValid() && player.IsValid() && player == Instigator )
		{
			return;
		}

		OnHit( collision );
	}

	protected virtual void OnHit( Collision collision = default )
	{
		GameObject.Destroy();
	}

	/// <summary>
	/// Tell the instigator their projectile hit, so they can do things like show a hitmarker
	/// </summary>
	protected void NotifyHit( IDamageable target, Vector3 point )
	{
		if ( !Instigator.IsValid() ) return;

		var connection = Instigator.Network.Owner;
		if ( connection is null ) return;

		// TODO: implement me
	}

	/// <summary>
	/// Spawns the medium explosion here, credited to the <see cref="Instigator"/>, and destroys the
	/// projectile. Host only - the explosion is network spawned.
	/// </summary>
	protected void Explode( float radius, float damage, float forceScale )
	{
		var explosionPrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/explosion_med.prefab" );
		if ( explosionPrefab == null )
		{
			Log.Warning( $"{GetType().Name}: Can't find /prefabs/engine/explosion_med.prefab" );
			GameObject.Destroy();
			return;
		}

		var go = GameObject.Clone( explosionPrefab, new CloneConfig { Transform = WorldTransform.WithScale( 1 ), StartEnabled = false } );
		if ( go.IsValid() )
		{
			go.RunEvent<RadiusDamage>( x =>
			{
				x.Radius = radius;
				x.PhysicsForceScale = forceScale;
				x.DamageAmount = damage;
				x.Attacker = Instigator?.GameObject;
				x.DamageTags ??= new();
				x.DamageTags.Add( DamageTags.Explosion );
			}, FindMode.EverythingInSelfAndDescendants );

			go.Enabled = true;
			go.NetworkSpawn( true, null );
		}

		GameObject.Destroy();
	}

	/// <summary>
	/// Updates the current direction of the projectile
	/// </summary>
	/// <param name="direction"></param>
	/// <param name="speed"></param>
	public void UpdateDirection( Vector3 direction, float speed )
	{
		WorldRotation = Rotation.LookAt( direction, Vector3.Up );
		Rigidbody.Velocity = WorldRotation.Forward * speed;
	}
}
