/// <summary>
/// A pickup that gives the player ammo.
/// </summary>
public sealed class AmmoPickup : BasePickup
{
	/// <summary>
	/// Which ammo <see cref="AmmoResource"/> we give the player
	/// </summary>
	[Property, Group( "Ammo" )] public AmmoResource AmmoResource { get; set; }

	/// <summary>
	/// The quantity of ammo
	/// </summary>
	[Property, Group( "Ammo" )] public int AmmoAmount { get; set; }

	public override bool CanPickup( Player player, PlayerInventory inventory )
	{
		return player.GetAmmoCount( AmmoResource ) < AmmoResource.MaxAmount;
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		player.GiveAmmo( AmmoResource, AmmoAmount, true );
		player.PlayerData.AddStat( $"pickup.ammo.{AmmoResource.AmmoType}" );

		return true;
	}
}
