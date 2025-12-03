using Sandbox.Rendering;

public class Mp5Weapon : BaseBulletWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 0.1f;
	[Property] public float Damage { get; set; } = 12.0f;
	[Property] public GameObject ProjectilePrefab { get; set; }

	protected TimeSince TimeSinceShoot = 0;

	protected override float GetPrimaryFireRate() => TimeBetweenShots;

	public override bool CanPrimaryAttack()
	{
		if ( !HasAmmo() ) return false;
		if ( IsReloading() ) return false;
		if ( TimeUntilNextShotAllowed > 0 ) return false;

		return true;
	}

	public override void PrimaryAttack()
	{
		ShootBullet();
	}

	private void ShootBullet()
	{
		if ( !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 )
		{
			TryAutoReload();
			return;
		}

		if ( !TakeAmmo( 1 ) )
		{
			AddShootDelay( 0.2f );
			return;
		}

		AddShootDelay( TimeBetweenShots );

		var aimConeAmount = GetAimConeAmount();
		var forward = Actor.EyeTransform.Rotation.Forward.WithAimCone( 0.5f + aimConeAmount * 4f, 0.25f + aimConeAmount * 4f );
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

		Owner.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.1f, -0.3f ), Random.Shared.Float( -0.1f, 0.1f ), 0 );

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			new Sandbox.CameraNoise.Recoil( 1.0f, 1 );
		}
	}

	// returns 0 for no aim spread, 1 for full aim cone
	float GetAimConeAmount()
	{
		return TimeSinceShoot.Relative.Remap( 0, 0.2f, 1, 0 );
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
