using Sandbox.Rendering;

public partial class BaseWeapon : BaseCarryable, IPlayerControllable
{
	/// <summary>
	/// How long after deploying a weapon can you not shoot a gun?
	/// </summary>
	[Property] public float DeployTime { get; set; } = 0.5f;

	public override bool ShouldAvoid => !HasAmmo();

	/// <summary>
	/// How long until we can shoot again
	/// </summary>
	protected TimeUntil TimeUntilNextShotAllowed;

	/// <summary>
	/// Adds a delay, making it so we can't shoot for the specified time
	/// </summary>
	/// <param name="seconds"></param>
	public void AddShootDelay( float seconds )
	{
		TimeUntilNextShotAllowed = seconds;
	}

	/// <summary>
	/// The dry fire sound if we have no ammo
	/// </summary>
	private static SoundEvent DryFireSound = new SoundEvent( "audio/sounds/dry_fire.sound" );

	/// <summary>
	/// Play a dry fire sound. You should only call this on weapons that can't auto reload - if they can, use <see cref="TryAutoReload"/> instead.
	/// </summary>
	public void DryFire()
	{
		if ( HasAmmo() )
			return;

		if ( IsReloading() )
			return;

		if ( TimeUntilNextShotAllowed > 0 )
			return;

		GameObject.PlaySound( DryFireSound );
	}

	/// <summary>
	/// Player has fired an empty gun - play dry fire sound and start reloading. You should only call this on weapons that can reload - if they can't, use <see cref="DryFire"/> instead.
	/// </summary>
	public virtual void TryAutoReload()
	{
		if ( HasAmmo() )
			return;

		if ( IsReloading() )
			return;

		if ( TimeUntilNextShotAllowed > 0 )
			return;

		DryFire();

		AddShootDelay( 0.1f );

		if ( CanReload() )
			OnReloadStart();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		AddShootDelay( DeployTime );
	}

	public override void OnAdded( Player player )
	{
		base.OnAdded( player );

		if ( !UsesAmmo )
			return;

		if ( AmmoType is not null )
		{
			// Seed the shared pool with the resource's default if the player has none yet
			var inv = GetAmmoInventory();
			if ( inv is not null && !inv.HasAmmo( AmmoType ) && AmmoType.DefaultStartingAmmo > 0 )
				inv.AddAmmo( AmmoType, AmmoType.DefaultStartingAmmo );
		}
		else if ( StartingAmmo > 0 )
		{
			_reserveAmmo = Math.Min( StartingAmmo, _maxReserveAmmo );
		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		DrawCrosshair( painter, crosshair );
	}

	public override void OnPlayerUpdate( Player player )
	{
		if ( player is null ) return;

		if ( !player.Controller.ThirdPerson )
		{
			CreateViewModel();
		}
		else
		{
			DestroyViewModel();
		}

		GameObject.Network.Interpolation = false;

		if ( !player.IsLocalPlayer )
			return;

		OnControl( player );
	}

	public override void OnControl( Player player )
	{
		bool wantsToCancelReload = Input.Pressed( "Attack1" ) || Input.Pressed( "Attack2" );
		if ( CanCancelReload && IsReloading() && wantsToCancelReload && HasAmmo() )
		{
			CancelReload();
		}

		if ( CanReload() && Input.Pressed( "reload" ) )
		{
			OnReloadStart();
		}

		if ( CanPrimaryAttack() && WantsPrimaryAttack() )
		{
			PrimaryAttack();
		}

		if ( CanSecondaryAttack() && WantsSecondaryAttack() )
		{
			SecondaryAttack();
		}
	}

	protected virtual bool WantsSecondaryAttack()
	{
		return Input.Down( "attack2" );
	}

	protected virtual bool WantsPrimaryAttack()
	{
		return Input.Down( "attack1" );
	}

	/// <summary>
	/// Override to perform the weapon's primary attack. Default no-op.
	/// </summary>
	public virtual void PrimaryAttack()
	{
	}

	/// <summary>
	/// Override to perform the weapon's secondary attack. Default no-op.
	/// </summary>
	public virtual void SecondaryAttack()
	{
	}

	/// <summary>
	/// Determines if the primary attack should trigger
	/// </summary>
	public virtual bool CanPrimaryAttack()
	{
		if ( HasOwner && !HasAmmo() ) return false;
		if ( IsReloading() ) return false;
		if ( TimeUntilNextShotAllowed > 0 ) return false;

		return true;
	}

	/// <summary>
	/// Determines if the secondary attack should trigger
	/// </summary>
	public virtual bool CanSecondaryAttack()
	{
		if ( HasOwner && !HasAmmo() ) return false;
		if ( IsReloading() ) return false;
		if ( TimeUntilNextShotAllowed > 0 ) return false;

		return true;
	}

	/// <summary>
	/// Override the primary fire rate
	/// </summary>
	protected virtual float GetPrimaryFireRate() => 0.1f;

	/// <summary>
	/// Override the secondary fire rate
	/// </summary>
	protected virtual float GetSecondaryFireRate() => 0.2f;

	/// <summary>
	/// The input that fires the primary attack when this weapon is controlled via a seat.
	/// </summary>
	[Property, Sync, ClientEditable, Group( "Inputs" )] public ClientInput ShootInput { get; set; }

	/// <summary>
	/// The input that fires the secondary attack when this weapon is controlled via a seat.
	/// </summary>
	[Property, Sync, ClientEditable, Group( "Inputs" )] public ClientInput SecondaryInput { get; set; }

	public void OnStartControl() { }

	public void OnEndControl() { }

	public virtual void OnControl()
	{
		if ( HasOwner ) return;

		if ( ShootInput.Down() && CanPrimaryAttack() )
			PrimaryAttack();

		if ( SecondaryInput.Down() && CanSecondaryAttack() )
			SecondaryAttack();
	}

	public virtual void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = Color.Red;

		hud.DrawLine( center + Vector2.Left * 32, center + Vector2.Left * 15, 3, color );
		hud.DrawLine( center - Vector2.Left * 32, center - Vector2.Left * 15, 3, color );
		hud.DrawLine( center + Vector2.Up * 32, center + Vector2.Up * 15, 3, color );
		hud.DrawLine( center - Vector2.Up * 32, center - Vector2.Up * 15, 3, color );
	}
	protected Color CrosshairCanShoot => Color.White;
	protected Color CrosshairNoShoot => Color.Red;
}
