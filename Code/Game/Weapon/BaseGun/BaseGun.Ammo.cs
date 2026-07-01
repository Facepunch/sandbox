public partial class BaseGun
{
	//
	// The generic ammo config (UsesAmmo, UsesClips, ClipMaxSize, StartingAmmo) and the magazine/reserve
	// plumbing live on the engine BaseWeapon now. This is just the sandbox AmmoResource layer mapped onto
	// it, plus the cheat convars.
	//

	/// <summary>The ammo resource this weapon's reserve draws from. Weapons sharing a resource share reserve.</summary>
	[Property, Feature( "Ammo" )] public AmmoResource AmmoType { get; set; }

	/// <summary>Max reserve this weapon can hold (from the resource).</summary>
	public int MaxReserveAmmo => AmmoType?.MaxReserve ?? 0;

	/// <summary>Point the engine's reserve pool at our ammo resource. Runs on every peer from the serialized config.</summary>
	protected override void OnAwake()
	{
		base.OnAwake();

		PrimaryAmmoType = (UsesAmmo && AmmoType is not null) ? AmmoType.ResourcePath : "";
	}

	/// <summary>Take ammo for a shot - from the magazine, or reserve for a clipless weapon. Honours the cheat convars.</summary>
	public bool TakeAmmo( int count )
	{
		if ( WeaponConVars.UnlimitedAmmo )
			return true;

		if ( UsesPrimaryClip )
		{
			if ( Clip1 < count ) return false;
			Clip1 -= count;
			return true;
		}

		if ( WeaponConVars.InfiniteReserves )
			return true;

		return TakePrimaryAmmo( count );
	}

	/// <summary>
	/// Convar-aware ammo check. Overrides the engine's so the host's FirePrimary gate honours the cheat
	/// convars too (not just the pump's CanPrimaryAttack).
	/// </summary>
	public override bool HasPrimaryAmmo()
	{
		if ( WeaponConVars.UnlimitedAmmo )
			return true;

		if ( UsesPrimaryClip )
			return Clip1 > 0;

		if ( WeaponConVars.InfiniteReserves )
			return true;

		return base.HasPrimaryAmmo();
	}

	/// <summary>Do we have a round ready to fire? Alias for the convar-aware <see cref="HasPrimaryAmmo"/>.</summary>
	public bool HasAmmo() => HasPrimaryAmmo();

	/// <summary>Add reserve ammo to the shared pool, clamped to the resource's max. Returns the amount added.</summary>
	public int AddReserveAmmo( int count )
	{
		if ( AmmoType is null || Inventory is null )
			return 0;

		var space = MaxReserveAmmo - Ammo1;
		var toAdd = Math.Min( count, space );
		if ( toAdd <= 0 )
			return 0;

		Inventory.GiveAmmo( AmmoType.ResourcePath, toAdd );
		return toAdd;
	}
}
