/// <summary>
/// A pickup that gives an inventory item, like a weapon
/// </summary>
public sealed class InventoryPickup : BasePickup
{
	/// <summary>
	/// A list of prefabs (that have to be inventory items) that are given to the player
	/// </summary>
	[Property, Group( "Inventory" )] public List<GameObject> Items { get; set; }

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( Items == null ) return false;

		bool consumed = false;
		foreach ( var prefab in Items )
		{
			if ( inventory.Pickup( prefab ) )
			{
				consumed = true;
				player.PlayerData.AddStat( $"pickup.inventory.{prefab.Name}" );
			}
		}

		return consumed;
	}
}
