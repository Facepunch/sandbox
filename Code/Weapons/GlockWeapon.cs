using Sandbox.Rendering;

public class GlockWeapon : BaseBulletWeapon
{
	[Property] public float Damage { get; set; } = 12.0f;
	[Property] public float PrimaryFireRate { get; set; } = 0.15f;
	[Property] public float SecondaryFireRate { get; set; } = 0.2f;

	protected TimeSince TimeSinceShoot = 0f;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;
	protected override float GetSecondaryFireRate() => SecondaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		ShootBullet( false, PrimaryFireRate );
	}

	public override void SecondaryAttack()
	{
		ShootBullet( true, SecondaryFireRate );
	}

	private void ShootBullet( bool secondary, float fireRate )
	{
		// Primary/secondary gating already handled by CanPrimary/CanSecondary,
		// but still respect auto-reload and ammo consumption here.
		if ( !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 )
		{
			TryAutoReload();
			return;
		}

		if ( !TakeAmmo( 1 ) )
			return;

		AddShootDelay( fireRate );

		var aimConeAmount = GetAimConeAmount();
		if ( secondary ) aimConeAmount *= 2; // Secondary fire has more spread

		var forward = Actor.EyeTransform.Rotation.Forward.WithAimCone( 0.1f + aimConeAmount * 3f, 0.1f + aimConeAmount * 3f );
		var bulletRadius = 1;

		var tr = Scene.Trace.Ray( Actor.EyeTransform.ForwardRay with { Forward = forward }, 4096 )
							.IgnoreGameObjectHierarchy( Actor.GameObject )
							.WithoutTags( "playercontroller" ) // don't hit playercontroller colliders
							.Radius( bulletRadius )
							.UseHitboxes()
							.Run();

		ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
		TraceAttack( TraceAttackInfo.From( tr, Damage ) );
		TimeSinceShoot = 0;

		if ( !Owner.IsValid() )
		{
			return;
		}

		Owner.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.2f, -0.5f ), Random.Shared.Float( -1, 1 ) * 0.4f, 0 );

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			_ = new Sandbox.CameraNoise.Recoil( 1f, 0.3f );
		}
	}

	// returns 0 for no aim spread, 1 for full aim cone
	float GetAimConeAmount()
	{
		return TimeSinceShoot.Relative.Remap( 0, 0.5f, 1, 0 );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		Color color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Normal );
		hud.DrawCircle( center, 5, Color.Black );
		hud.DrawCircle( center, 3, color );
	}
}
