[Title( "Ironwood .357" )]
public sealed class RevolverWeapon : IronSightsWeapon
{
	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	protected override void DoRecoil()
	{
		base.DoRecoil();

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			Scene.Camera?.AddPunch( new Angles(
			-30,
			0,
			0 ), 1, 1.2f );
		}
	}

	/// <summary>
	/// Draws the outlined aiming dot with the current firing status.
	/// </summary>
	public override void DrawCrosshair( Painter painter, Vector2 center )
	{
		using var scope = painter.Scope();
		var color = !HasPrimaryAmmo() || IsReloading || NextPrimaryFire > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		painter.BlendMode = BlendMode.Normal;
		painter.Fill = color;
		painter.Stroke = Stroke.Solid( Color.Black, 1f ).WithAlignment( Stroke.StrokeAlignment.Outside );
		painter.Circle( center, 1.5f );
	}
}
