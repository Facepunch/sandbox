public sealed class DroppedWeapon : Component, Component.IPressable, PlayerController.IEvents
{
	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return null;
		return new IPressable.Tooltip( "Pick up", "inventory_2", weapon.DisplayName.ToUpper() );
	}

	bool IPressable.Press( IPressable.Event e )
	{
		DoPickup( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	private void DoPickup( GameObject presserObject )
	{
		if ( !presserObject.IsValid() ) return;

		var player = presserObject.Root.GetComponent<Player>();
		if ( !player.IsValid() ) return;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		TakeIntoInventory( inventory );
	}

	/// <summary>
	/// Disables world-physics components and moves the weapon into the player's inventory.
	/// </summary>
	private void TakeIntoInventory( PlayerInventory inventory )
	{
		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return;

		Enabled = false;

		inventory.Take( weapon, true );
	}
}
