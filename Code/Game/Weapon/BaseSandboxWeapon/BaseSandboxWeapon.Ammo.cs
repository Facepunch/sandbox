public partial class BaseSandboxWeapon
{
	//
	// The sandbox AmmoResource layer mapped onto the engine's ammo plumbing. The generic config
	// (UsesAmmo, UsesClips, ClipMaxSize, StartingAmmo) and the magazine/reserve live on the engine
	// BaseWeapon.
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

	/// <summary>
	/// Unheld weapons (seats, world) never run dry - their magazine is never seeded.
	/// </summary>
	public override bool HasPrimaryAmmo()
	{
		if ( !HasOwner )
			return true;

		return base.HasPrimaryAmmo();
	}

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
