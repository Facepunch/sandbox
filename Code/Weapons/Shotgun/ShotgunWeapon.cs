using Sandbox.Rendering;

public class ShotgunWeapon : IronSightsWeapon
{
	[Property] public float PrimaryFireRate { get; set; } = 0.8f;
	[Property] public int PelletCount { get; set; } = 8;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		if ( HasOwner && ( !HasAmmo() || IsReloading() ) )
		{
			TryAutoReload();
			return;
		}

		if ( TimeUntilNextShotAllowed > 0 )
			return;

		if ( HasOwner && !TakeAmmo( 1 ) )
		{
			AddShootDelay( 0.2f );
			return;
		}

		AddShootDelay( PrimaryFireRate );

		var eyeForward = AimRay.Forward;
		var eyeRay = AimRay;

		for ( var i = 0; i < PelletCount; i++ )
		{
			var aimConeAmount = GetAimConeAmount();
			var forward = eyeForward
				.WithAimCone(
					Bullet.AimConeBase.x + aimConeAmount * Bullet.AimConeSpread.x,
					Bullet.AimConeBase.y + aimConeAmount * Bullet.AimConeSpread.y
				);

			var tr = Scene.Trace.Ray( eyeRay with { Forward = forward }, Bullet.Range )
				.IgnoreGameObjectHierarchy( AimIgnoreRoot )
				.WithoutTags( "playercontroller" )
				.Radius( Bullet.BulletRadius )
				.UseHitboxes()
				.Run();

			ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface, noEvents: i > 0 );
			TraceAttack( TraceAttackInfo.From( tr, Bullet.Damage ) );
		}

		TimeSinceShoot = 0;

		if ( !HasOwner )
		{
			if ( ShootForce > 0f && GetComponent<Rigidbody>( true ) is var rb )
			{
				var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;
				rb.ApplyForce( muzzle.Rotation.Up * ShootForce );
			}
			return;
		}

		Owner.Controller.EyeAngles += new Angles(
			Random.Shared.Float( Bullet.RecoilPitch.x, Bullet.RecoilPitch.y ),
			Random.Shared.Float( Bullet.RecoilYaw.x, Bullet.RecoilYaw.y ),
			0
		);

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			_ = new Sandbox.CameraNoise.Recoil( Bullet.CameraRecoilStrength, Bullet.CameraRecoilFrequency );
		}
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var spread = GetAimConeAmount();
		var radius = 20 + spread * 40;

		var color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );

		const int segments = 32;
		for ( var i = 0; i < segments; i++ )
		{
			var a1 = MathF.PI * 2f * i / segments;
			var a2 = MathF.PI * 2f * (i + 1) / segments;
			var p1 = center + new Vector2( MathF.Cos( a1 ), MathF.Sin( a1 ) ) * radius;
			var p2 = center + new Vector2( MathF.Cos( a2 ), MathF.Sin( a2 ) ) * radius;
			hud.DrawLine( p1, p2, 2f, color );
		}

		hud.DrawCircle( center, 3, color );
	}
}
