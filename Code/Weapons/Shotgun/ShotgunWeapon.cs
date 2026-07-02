using Sandbox.Rendering;

public sealed class ShotgunWeapon : IronSightsWeapon
{
	[Property] public int PelletCount { get; set; } = 8;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		if ( HasOwner && ( !HasAmmo() || IsReloading ) )
		{
			TryAutoReload();
			return;
		}

		// Cooldown already gated by the caller before PrimaryAttack runs.

		if ( HasOwner && !TakeAmmo( 1 ) )
		{
			AddShootDelay( 0.2f );
			return;
		}

		AddShootDelay( PrimaryDelay );

		// One volley through the engine - a BulletTrace per pellet, damage host-authoritative
		// (ShootBullets self-gates, the owner's predicted run just gets the traces for effects).
		var pellets = ShootBullets( PelletCount, GetAimCone( Bullet ), Bullet.Range, Bullet.BulletRadius, Bullet.Damage, HitForce );

		// Effects predict on the owner and are relayed by the host. Muzzle/anim events fire on the
		// first pellet only - every pellet still gets its tracer and impact.
		for ( var i = 0; i < pellets.Length; i++ )
		{
			var tr = pellets[i];
			ShootEffects( new ShotEffect( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface, NoEvents: i > 0 ) );
		}

		TimeSinceShoot = 0;

		if ( !HasOwner )
		{
			if ( ShootForce > 0f && GetComponent<Rigidbody>( true ) is { } rb )
			{
				var muzzle = WeaponModel?.MuzzleGameObject?.WorldTransform ?? WorldTransform;
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

		var color = !HasAmmo() || IsReloading || NextPrimaryFire > 0 ? CrosshairNoShoot : CrosshairCanShoot;

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
