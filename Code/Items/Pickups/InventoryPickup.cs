/// <summary>
/// A pickup that gives an inventory item, like a weapon
/// </summary>
public sealed class InventoryPickup : BasePickup, Component.IPressable
{
	/// <summary>
	/// A list of prefabs (that have to be inventory items) that are given to the player
	/// </summary>
	[Property, Group( "Inventory" )] public List<GameObject> Items { get; set; }

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( Items == null || Items.Count == 0 ) return null;
		return new IPressable.Tooltip( "Pick up", "inventory_2", string.Join( ", ", Items.Select( i => i.Name.ToUpper() ) ) );
	}

	public bool Press( IPressable.Event e )
	{
		var player = e.Source.GameObject.Root.Components.Get<Player>();
		if ( !player.IsValid() ) return false;
		if ( OnPickup( player, player.GetComponent<PlayerInventory>() ) )
		{
			GameObject.Destroy();
			return true;
		}

		return false;
	}

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

		Log.Info( $"on pickup, {consumed}" );

		return consumed;
	}
}
