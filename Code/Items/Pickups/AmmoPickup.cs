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
		if ( AmmoType is not null )
		{
			var ammoInv = player.GetComponent<AmmoInventory>();
			if ( ammoInv is null ) return false;
			return ammoInv.GetAmmo( AmmoType ) < AmmoType.MaxReserve;
		}

		return false;
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is not null )
		{
			var ammoInv = player.GetComponent<AmmoInventory>();
			if ( ammoInv is null ) return false;
			return ammoInv.AddAmmo( AmmoType, AmmoAmount ) > 0;
		}

		return true;
	}
}
