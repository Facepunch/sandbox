public partial class BaseBulletWeapon : BaseGun
{
	[Property, Group( "Bullet" )]
	public BulletConfiguration Bullet { get; set; } = new()
	{
		Damage = 12f,
		BulletRadius = 1f,
		Range = 4096f,
		AimConeBase = new Vector2( 0.5f, 0.25f ),
		AimConeSpread = new Vector2( 3f, 3f ),
		AimConeRecovery = 0.2f,
		RecoilPitch = new Vector2( -0.3f, -0.1f ),
		RecoilYaw = new Vector2( -0.1f, 0.1f ),
		CameraRecoilStrength = 1f,
		CameraRecoilFrequency = 1f,
	};

	[Property, Group( "Bullet" ), ClientEditable, Range( 0f, 500000f ), Step( 10f )]
	public float ShootForce { get; set; } = 100000f;

	/// <summary>
	/// Impulse applied to the physics body a bullet hits, along the shot direction.
	/// </summary>
	[Property, Group( "Bullet" )]
	public float HitForce { get; set; } = 3000f;

	protected TimeSince TimeSinceShoot = 0;

	/// <summary>
	/// Returns 0 for no aim spread, 1 for full aim cone, based on time since last shot.
	/// </summary>
	protected float GetAimConeAmount( float recovery )
	{
		return TimeSinceShoot.Relative.Remap( 0, recovery, 1, 0 );
	}

	/// <summary>
	/// The current spread cone in degrees - the base cone widened by how recently we fired.
	/// </summary>
	protected Vector2 GetAimCone( in BulletConfiguration config )
	{
		var amount = GetAimConeAmount( config.AimConeRecovery );

		return new Vector2(
			config.AimConeBase.x + amount * config.AimConeSpread.x,
			config.AimConeBase.y + amount * config.AimConeSpread.y );
	}

	/// <summary>
	/// Bullet trace for this gun - the sandbox's bullet collision rules, skipping player controller
	/// colliders (players are hit through their hitboxes).
	/// </summary>
	protected override SceneTrace BulletTrace( Ray ray, float distance, float radius )
	{
		return Scene.Trace.Ray( ray, distance )
			.IgnoreGameObjectHierarchy( AimIgnoreRoot )
			.WithCollisionRules( "bullet" )
			.WithoutTags( "playercontroller" )
			.Radius( radius )
			.UseHitboxes();
	}

	/// <summary>
	/// Returns the aim cone amount using the configured recovery time
	/// </summary>
	protected float GetAimConeAmount()
	{
		return GetAimConeAmount( Bullet.AimConeRecovery );
	}

	/// <inheritdoc cref="ShootBullet(in BulletConfiguration)"/>
	protected void ShootBullet()
	{
		ShootBullet( Bullet );
	}

	/// <summary>
	/// Shoot a bullet out of the front of the gun. Fire rate comes from the engine's PrimaryDelay.
	/// When held by a player, fires from the player's eye with aim cone and recoil.
	/// When standalone (no owner), fires straight from the weapon's muzzle.
	/// </summary>
	protected void ShootBullet( in BulletConfiguration config )
	{
		if ( HasOwner && ( !HasAmmo() || IsReloading ) )
		{
			TryAutoReload();
			return;
		}

		// Cooldown is gated by the caller (the engine fire loop / CanPrimaryAttack) before we get here -
		// FirePrimary has already set NextPrimaryFire by this point, so we must not re-check it.

		// Only consume ammo when held by a player
		if ( HasOwner && !TakeAmmo( 1 ) )
		{
			AddShootDelay( 0.2f );
			return;
		}

		AddShootDelay( PrimaryDelay );

		var spread = GetAimCone( config );
		var traceRay = AimRay with { Forward = AimRay.Forward.WithAimCone( spread.x, spread.y ) };

		var tr = BulletTrace( traceRay, config.Range, config.BulletRadius ).Run();

		// Effects predict on the owner and are relayed by the host (BaseWeapon.ShootEffects owns that
		// netcode). Damage and the prop push are host-authoritative - ShootBullet self-gates, so the
		// owner's predicted run gets the trace back and deals nothing.
		ShootEffects( new ShotEffect( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface ) );

		ShootBullet( tr, config.Damage, HitForce, Attacker, GameObject );

		TimeSinceShoot = 0;

		// Recoil only applies when held by a player
		if ( !HasOwner )
		{
			// Simulate physical recoil by pushing the weapon opposite to its fire direction
			if ( ShootForce > 0f && GetComponent<Rigidbody>( true ) is { } rb )
			{
				var muzzle = WeaponModel?.MuzzleGameObject?.WorldTransform ?? WorldTransform;
				rb.ApplyForce( muzzle.Rotation.Up * ShootForce );
			}
			return;
		}

		Owner.Controller.EyeAngles += new Angles(
			Random.Shared.Float( config.RecoilPitch.x, config.RecoilPitch.y ),
			Random.Shared.Float( config.RecoilYaw.x, config.RecoilYaw.y ),
			0
		);

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			_ = new Sandbox.CameraNoise.Recoil( config.CameraRecoilStrength, config.CameraRecoilFrequency );
		}
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		// Attack sound, holder anim and the weapon model's muzzle/brass/tracer come from the base.
		base.OnShootEffects( shot );

		// Surface impact at the hit - every pellet leaves one.
		ImpactEffect( shot );
	}

	public record struct BulletConfiguration
	{
		public float Damage { get; set; }
		public float BulletRadius { get; set; }
		public Vector2 AimConeBase { get; set; }
		public Vector2 AimConeSpread { get; set; }
		public float AimConeRecovery { get; set; }
		public Vector2 RecoilPitch { get; set; }
		public Vector2 RecoilYaw { get; set; }
		public float CameraRecoilStrength { get; set; }
		public float CameraRecoilFrequency { get; set; }
		public float Range { get; set; }
	}
}
