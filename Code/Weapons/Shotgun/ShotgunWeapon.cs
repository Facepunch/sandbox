using Sandbox.Rendering;

public sealed class ShotgunWeapon : IronSightsWeapon
{
	// The volley (pellet count, spread, damage) comes from the engine Ballistics; recoil from
	// BaseBulletWeapon.

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var spread = SpreadBloom;
		var radius = 20 + spread * 40;

		var color = !HasPrimaryAmmo() || IsReloading || NextPrimaryFire > 0 ? CrosshairNoShoot : CrosshairCanShoot;

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
