using Sandbox.Rendering;

public class GlockWeapon : BaseBulletWeapon
{
	[Property] public float PrimaryFireRate { get; set; } = 0.15f;
	[Property] public float SecondaryFireRate { get; set; } = 0.2f;
	[Property] public float SecondarySpreadMultiplier { get; set; } = 2f;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;
	protected override float GetSecondaryFireRate() => SecondaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		ShootBullet( PrimaryFireRate );
	}

	public override void SecondaryAttack()
	{
		var config = Bullet;
		config.AimConeSpread *= SecondarySpreadMultiplier;
		ShootBullet( SecondaryFireRate, config );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Normal );
		hud.DrawCircle( center, 5, Color.Black );
		hud.DrawCircle( center, 3, color );
	}
}
