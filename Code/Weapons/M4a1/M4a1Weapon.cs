using Sandbox.Rendering;

public class M4a1Weapon : BaseBulletWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 0.1f;

	protected override float GetPrimaryFireRate() => TimeBetweenShots;

	public override void PrimaryAttack()
	{
		ShootBullet( TimeBetweenShots );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var gap = 16 + GetAimConeAmount() * 32;
		var len = 12;
		var w = 2f;

		var color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawLine( center + Vector2.Left * (len + gap), center + Vector2.Left * gap, w, color );
		hud.DrawLine( center - Vector2.Left * (len + gap), center - Vector2.Left * gap, w, color );
		hud.DrawLine( center + Vector2.Up * (len + gap), center + Vector2.Up * gap, w, color );
		hud.DrawLine( center - Vector2.Up * (len + gap), center - Vector2.Up * gap, w, color );
	}
}
