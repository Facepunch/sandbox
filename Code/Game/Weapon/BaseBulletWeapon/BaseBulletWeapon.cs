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

	/// <summary>
	/// Physical kick applied to a standalone (unheld) gun when it fires.
	/// </summary>
	[Property, Group( "#weapon.group.recoil" ), ClientEditable, Range( 0f, 500000f ), Step( 10f ), Title( "#weapon.setting.recoil_force" ), Description( "#weapon.setting.recoil_force.description" )]
	public float ShootForce { get; set; } = 100000f;

	/// <summary>
	/// Ragdolls take the bullet as a velocity change rather than a raw impulse, so a torso shot moves the
	/// body as visibly as a hand shot moves the hand. The struck bone gains <c>Ballistics.Force * RagdollImpactScale</c>
	/// units/sec along the shot; every other bone gains <see cref="RagdollBodyShare"/> of that.
	/// </summary>
	[Property, Group( "Ragdoll Impact" ), Range( 0f, 10f )] public float RagdollImpactScale { get; set; } = 1f;

	/// <summary>Fraction of the struck bone's velocity change given to the rest of the ragdoll.</summary>
	[Property, Group( "Ragdoll Impact" ), Range( 0f, 1f )] public float RagdollBodyShare { get; set; } = 0.3f;

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
	/// Runs for every pellet on every peer - death ragdolls are local to each peer, so the push has to be
	/// applied wherever the body is simulated, not just on the host.
	/// </summary>
	protected override void OnShootEffects( ShotEffect shot )
	{
		base.OnShootEffects( shot );

		if ( shot.Hit )
			PushRagdoll( shot.HitObject, shot.HitPosition, shot.Origin ?? GetMuzzleTransform().Position );
	}

	void PushRagdoll( GameObject hitObject, Vector3 hitPosition, Vector3 origin )
	{
		if ( !hitObject.IsValid() ) return;

		var struck = hitObject.Components.GetInAncestorsOrSelf<Rigidbody>();
		if ( !struck.IsValid() || struck.IsProxy ) return;

		var physics = hitObject.Components.GetInAncestorsOrSelf<ModelPhysics>();
		if ( !physics.IsValid() || physics.Bodies.Count < 2 ) return;

		var speed = Ballistics.Force * RagdollImpactScale;
		if ( speed <= 0f ) return;

		var dir = (hitPosition - origin).Normal;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;

			if ( rb == struck )
				rb.ApplyImpulseAt( hitPosition, dir * speed * rb.Mass );
			else if ( RagdollBodyShare > 0f )
				rb.ApplyImpulse( dir * speed * RagdollBodyShare * rb.Mass );
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
