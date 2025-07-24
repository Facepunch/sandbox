using Sandbox.Rendering;

public class GlockWeapon : BaseBulletWeapon
{
	[Property] public float Damage { get; set; } = 12.0f;
	[Property] public float PrimaryFireRate { get; set; } = 0.15f;
	[Property] public float SecondaryFireRate { get; set; } = 0.2f;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		bool secondary = Input.Down( "attack2" );
		float fireRate = secondary ? SecondaryFireRate : PrimaryFireRate;

		if ( IsInputQueued( () => secondary ? secondary : Input.Pressed( "Attack1" ), fireRate ) )
		{
			ShootBullet( player, secondary, fireRate );
		}
	}

	public void ShootBullet( Player player, bool secondary, float fireRate )
	{
		if ( !CanShoot() )
		{
			TryAutoReload();
			return;
		}

		if ( !TakeAmmo( 1 ) )
			return;

		AddShootDelay( fireRate );

		var aimConeAmount = GetAimConeAmount();
		if ( secondary ) aimConeAmount *= 2; // Secondary fire has more spread

		var forward = player.EyeTransform.Rotation.Forward.WithAimCone( 0.1f + aimConeAmount * 3f, 0.1f + aimConeAmount * 3f );
		var bulletRadius = GameSettings.GlockBulletRadius * GameSettings.BulletRadius;

		var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay with { Forward = forward }, 4096 )
							.IgnoreGameObjectHierarchy( player.GameObject )
							.WithoutTags( "playercontroller" ) // don't hit playercontroller colliders
							.Radius( bulletRadius )
							.UseHitboxes()
							.Run();

		ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
		TraceAttack( TraceAttackInfo.From( tr, Damage ) );
		TimeSinceShoot = 0;

		player.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.2f, -0.5f ), Random.Shared.Float( -1, 1 ) * 0.4f, 0 );

		if ( !player.Controller.ThirdPerson && player.IsLocalPlayer )
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
		var gap = 10 + GetAimConeAmount() * 22;
		var len = 8;
		var w = 2f;

		Color color = !CanShoot() ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawLine( center + Vector2.Left * (len + gap), center + Vector2.Left * gap, w, color );
		hud.DrawLine( center - Vector2.Left * (len + gap), center - Vector2.Left * gap, w, color );
		hud.DrawLine( center + Vector2.Up * (len + gap), center + Vector2.Up * gap, w, color );
		hud.DrawCircle( center, w * 2f, color );
	}
}
