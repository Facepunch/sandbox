public partial class BaseBulletWeapon : BaseSandboxWeapon
{
	//
	// Ballistics (damage, pellets, spread, reach) and the attack itself live on the engine BaseCombatWeapon.
	// What's left here is the game feel - recoil and camera shake - and the sandbox's bullet collision
	// rules.
	//

	/// <summary>Random pitch punch per shot (min, max degrees).</summary>
	[Property, Group( "Recoil" )] public Vector2 RecoilPitch { get; set; } = new( -0.3f, -0.1f );

	/// <summary>Random yaw punch per shot (min, max degrees).</summary>
	[Property, Group( "Recoil" )] public Vector2 RecoilYaw { get; set; } = new( -0.1f, 0.1f );

	/// <summary>First-person camera shake strength per shot.</summary>
	[Property, Group( "Recoil" )] public float CameraRecoilStrength { get; set; } = 1f;

	/// <summary>First-person camera shake frequency per shot.</summary>
	[Property, Group( "Recoil" )] public float CameraRecoilFrequency { get; set; } = 1f;

	/// <summary>Physical kick applied to a standalone (unheld) gun when it fires.</summary>
	[Property, Group( "Recoil" ), ClientEditable, Range( 0f, 500000f ), Step( 10f )]
	public float ShootForce { get; set; } = 100000f;

	public override void PrimaryAttack()
	{
		base.PrimaryAttack();

		DoRecoil();
	}

	/// <summary>
	/// The per-shot kick - eye punch and camera shake for a held gun, a physical shove for a
	/// standalone one.
	/// </summary>
	protected virtual void DoRecoil()
	{
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
			Random.Shared.Float( RecoilPitch.x, RecoilPitch.y ),
			Random.Shared.Float( RecoilYaw.x, RecoilYaw.y ),
			0 );

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			// A quick roll wobble per shot.
			Scene.Camera?.AddPunch( new Angles( 0f, 0f, 0.5f * CameraRecoilStrength ), CameraRecoilFrequency * 2f, 0.3f );
		}
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

}
