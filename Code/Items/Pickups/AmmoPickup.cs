/// <summary>
/// A pickup that gives the player reserve ammo for a matching weapon.
/// </summary>
public sealed class AmmoPickup : BasePickup
{
	/// <summary>
	/// The ammo resource this pickup gives ammo for.
	/// When set, ammo is added directly to the player's shared pool for that resource.
	/// </summary>
	[Property, Group( "Ammo" )] public AmmoResource AmmoType { get; set; }

	/// <summary>
	/// The quantity of ammo to give.
	/// </summary>
	[Property, Group( "Ammo" )] public int AmmoAmount { get; set; }

	public override bool CanPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is null || inventory is null ) return false;
		return inventory.GetAmmo( AmmoType.ResourcePath ) < AmmoType.MaxReserve;
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is null || inventory is null ) return false;

		// The inventory pool isn't clamped, so cap to the resource's max here.
		var space = AmmoType.MaxReserve - inventory.GetAmmo( AmmoType.ResourcePath );
		if ( space <= 0 ) return false;

		inventory.GiveAmmo( AmmoType.ResourcePath, Math.Min( AmmoAmount, space ) );
		return true;
	}
}
