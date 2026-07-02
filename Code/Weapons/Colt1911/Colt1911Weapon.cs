using Sandbox.Rendering;

public sealed class Colt1911Weapon : IronSightsWeapon
{
	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		ShootBullet( GetBullet() );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = !HasPrimaryAmmo() || IsReloading || NextPrimaryFire > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Normal );
		hud.DrawCircle( center, 5, Color.Black );
		hud.DrawCircle( center, 3, color );
	}
}
