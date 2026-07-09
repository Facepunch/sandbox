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

	// Synced (owner-authoritative) so the host's authoritative shot trace sees it too - otherwise ADS
	// only narrows the cone on the owner's predicted (non-damaging) pass and the real shot stays wide.
	[Sync] public bool IsAiming { get; set; }

	public override bool CanSecondaryAttack() => false;

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( IsAiming ) return;
		base.DrawHud( painter, crosshair );
	}

	protected override void OnControl()
	{
		base.OnControl();

		var wantsAim = Input.Down( "attack2" );

		if ( wantsAim == IsAiming )
			return;

		IsAiming = wantsAim;
		ViewModel?.RunEvent<ViewModel>( x =>
		{
			x.Renderer?.Set( "ironsights", IsAiming ? 1 : 0 );
			x.Renderer?.Set( "ironsights_fire_scale", IsAiming ? IronSightsFireScale : 1f );
		} );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		IsAiming = false;
	}

	/// <summary>Aiming down sights narrows the spread cone.</summary>
	public override Vector2 CurrentSpread => base.CurrentSpread * (IsAiming ? IronSightsFireScale : 1f);
}
