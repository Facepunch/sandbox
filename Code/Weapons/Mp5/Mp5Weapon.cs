using Sandbox.Rendering;

public class Mp5Weapon : BaseBulletWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 0.1f;
	[Property] public float Damage { get; set; } = 12.0f;
	[Property] public GameObject ProjectilePrefab { get; set; }
	[Property] AmmoResource ProjectileAmmoResource { get; set; }

	bool _isShooting;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		_isShooting = Input.Down( "attack1" );
		if ( _isShooting )
		{
			if ( Input.Pressed( "Attack1" ) )
			{
				StartAttack();
			}

			ShootBullet( player );
		}
		else if ( Input.Released( "attack1" ) )
		{
			StopAttack();
		}
	}

	public override bool IsInUse() => _isShooting;

	/// <summary>
	/// How long until we can shoot again
	/// </summary>
	protected TimeUntil TimeUntilNextSecondaryShotAllowed;

	/// <summary>
	/// Adds a delay, making it so we can't shoot for the specified time
	/// </summary>
	/// <param name="seconds"></param>
	public void AddSecondaryShootDelay( float seconds )
	{
		TimeUntilNextSecondaryShotAllowed = seconds;
	}

	public override bool CanSwitch()
	{
		return base.CanSwitch() || HasAmmo( ProjectileAmmoResource );
	}


	public void ShootBullet( Player player )
	{
		if ( !CanShoot() )
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
		var forward = player.EyeTransform.Rotation.Forward.WithAimCone( 0.5f + aimConeAmount * 4f, 0.25f + aimConeAmount * 4f );
		var bulletRadius = 1;

		var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay with { Forward = forward }, 4096 )
							.IgnoreGameObjectHierarchy( player.GameObject )
							.WithoutTags( "playercontroller" ) // don't hit playercontroller colliders
							.Radius( bulletRadius )
							.UseHitboxes()
							.Run();

		Log.Info( $"{tr.Surface}" );
		Log.Info( $"{tr.Hitbox}" );

		ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
		TraceAttack( TraceAttackInfo.From( tr, Damage ) );
		TimeSinceShoot = 0;

		player.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.1f, -0.3f ), Random.Shared.Float( -0.1f, 0.1f ), 0 );

		if ( !player.Controller.ThirdPerson && player.IsLocalPlayer )
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

		var color = !CanShoot() ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawLine( center + Vector2.Left * (len + gap), center + Vector2.Left * gap, w, color );
		hud.DrawLine( center - Vector2.Left * (len + gap), center - Vector2.Left * gap, w, color );
		hud.DrawLine( center + Vector2.Up * (len + gap), center + Vector2.Up * gap, w, color );
		hud.DrawLine( center - Vector2.Up * (len + gap), center - Vector2.Up * gap, w, color );
	}

	Texture ammoIcon = Texture.Load( $"ui/bullet_icon.png" );

	public override void DrawAmmo( HudPainter hud, Vector2 bottomright )
	{
		base.DrawAmmo( hud, bottomright );

		var ammoCount = Owner.GetAmmoCount( ProjectileAmmoResource );
		hud.DrawHudElement( $"{ammoCount}", bottomright - (new Vector2( 0, 64f ) * Hud.Scale), ammoIcon, 30f, TextFlag.RightCenter );
	}
}
