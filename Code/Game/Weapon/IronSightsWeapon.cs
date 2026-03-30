using Sandbox.Rendering;

/// <summary>
/// A weapon that can aim down sights
/// </summary>
public abstract class IronSightsWeapon : BaseBulletWeapon
{
	/// <summary>
	/// Lowers the amount of recoil / visual noise when aiming
	/// </summary>
	[Property] public float IronSightsFireScale { get; set; } = 0.2f;

	private bool _isAiming;

	public bool IsAiming => _isAiming;

	public override bool CanSecondaryAttack() => false;

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( _isAiming ) return;
		base.DrawHud( painter, crosshair );
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		var wantsAim = Input.Down( "attack2" );

		if ( wantsAim == _isAiming )
			return;

		_isAiming = wantsAim;
		ViewModel?.RunEvent<ViewModel>( x =>
		{
			x.Renderer?.Set( "ironsights", _isAiming ? 1 : 0 );
			x.Renderer?.Set( "ironsights_fire_scale", _isAiming ? IronSightsFireScale : 1f );
		} );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		_isAiming = false;
	}

	protected BulletConfiguration GetBullet()
	{
		if ( !_isAiming )
			return Bullet;

		var config = Bullet;
		config.AimConeBase *= IronSightsFireScale;
		config.AimConeSpread *= IronSightsFireScale;
		return config;
	}
}
