using Sandbox.Rendering;

public partial class BaseGun : BaseCarryable, IPlayerControllable
{
	// Deploy time, dry fire, auto-reload, the fire/reload loop and the per-fire cooldown all live on the
	// engine BaseWeapon now. What's left here is the sandbox glue: the AmmoResource layer (BaseGun.Ammo),
	// the convar-aware ammo gates, seat control, and the HUD.

	public override bool ShouldAvoid => !HasAmmo();

	/// <summary>
	/// Adds a delay before this weapon can fire again. Maps to the engine's per-fire cooldown; a shot
	/// blocks both triggers (the sandbox uses a single shared cooldown).
	/// </summary>
	public void AddShootDelay( float seconds )
	{
		SetNextPrimaryFire( seconds );
		SetNextSecondaryFire( seconds );
	}

	public override void OnAdded( Player player )
	{
		base.OnAdded( player );

		if ( !Networking.IsHost )
			return;

		// Seed the magazine full.
		if ( UsesAmmo && UsesClips )
			Clip1 = ClipMaxSize;

		if ( !UsesAmmo || AmmoType is null )
			return;

		// Seed the shared reserve pool once - only when the player first gets a gun of this ammo type.
		// Guarding on the pool being empty stops two guns that share a resource from double-seeding it
		// (and pickup churn from inflating reserve over time).
		if ( !Inventory.HasAmmo( AmmoType.ResourcePath ) )
		{
			var seed = AmmoType.DefaultStartingAmmo + StartingAmmo;
			if ( seed > 0 )
				AddReserveAmmo( seed );
		}
	}

	/// <summary>
	/// Determines if the primary attack should trigger. Adds the convar-aware ammo gate on top of the
	/// engine's cooldown / reload checks.
	/// </summary>
	public override bool CanPrimaryAttack()
	{
		if ( HasOwner && !HasAmmo() ) return false;
		if ( IsReloading ) return false;
		if ( NextPrimaryFire > 0 ) return false;

		return true;
	}

	/// <inheritdoc cref="CanPrimaryAttack"/>
	public override bool CanSecondaryAttack()
	{
		if ( HasOwner && !HasAmmo() ) return false;
		if ( IsReloading ) return false;
		if ( NextSecondaryFire > 0 ) return false;

		return true;
	}

	//
	// Seat / contraption control (IPlayerControllable) - sandbox specific.
	//

	/// <summary>The input that fires the primary attack when this weapon is controlled via a seat.</summary>
	[Property, Sync, ClientEditable, Group( "Inputs" )] public ClientInput ShootInput { get; set; }

	/// <summary>The input that fires the secondary attack when this weapon is controlled via a seat.</summary>
	[Property, Sync, ClientEditable, Group( "Inputs" )] public ClientInput SecondaryInput { get; set; }

	public bool CanControl( Player player )
	{
		var inventory = player.GetComponent<PlayerInventory>();
		return inventory is null || !inventory.ActiveWeapon.IsValid();
	}

	public void OnStartControl() { }

	public void OnEndControl() { }

	// Seat / contraption control. Explicit interface impl so it doesn't clash with the engine's held-item
	// OnControl pump; subclasses override OnSeatControl to change the seated behaviour.
	void IPlayerControllable.OnControl() => OnSeatControl();

	protected virtual void OnSeatControl()
	{
		if ( HasOwner ) return;
		// Seat fire is fully host-authoritative - the host reads the driver's synced ClientInput and runs
		// the shot for real. No prediction on the driving client (unlike the held FirePrimary path).
		if ( !Networking.IsHost ) return;

		if ( ShootInput.Down() && CanPrimaryAttack() )
			PrimaryAttack();

		if ( SecondaryInput.Down() && CanSecondaryAttack() )
			SecondaryAttack();
	}

	// DrawHud / DrawCrosshair and the CrosshairCanShoot/CrosshairNoShoot colours come from the engine
	// BaseWeapon.
}
