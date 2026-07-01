using Sandbox.UI;

public sealed class DroppedWeapon : Component, Component.IPressable, PlayerController.IEvents
{
	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return null;

		var name = weapon.DisplayName.ToUpper();

		if ( HasInput() ) return new IPressable.Tooltip( "Can't pick this up", "block", name );
		if ( IsInventoryFull() ) return new IPressable.Tooltip( "Inventory Full", "block", name );
		return new IPressable.Tooltip( "Pick up", "inventory_2", name );
	}

	private bool IsInventoryFull()
	{
		var player = Player.FindLocalPlayer();
		if ( !player.IsValid() ) return false;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return false;

		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return false;

		return !inventory.CanTake( weapon );
	}

	private bool HasInput()
	{
		var weapon = GetComponent<BaseGun>();
		if ( !weapon.IsValid() ) return false;
		return weapon.ShootInput.IsEnabled || weapon.SecondaryInput.IsEnabled;
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		//
		// Can't pick up weapons that are fireable by a contraption
		//
		if ( HasInput() ) return false;

		if ( IsInventoryFull() ) return false;

		return true;
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

		if ( !Enabled ) return;

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

		if ( !inventory.Take( weapon, true ) )
		{
			ShowInventoryFull();
			return;
		}

		Enabled = false;
	}

	[Rpc.Owner]
	private void ShowInventoryFull()
	{
		Notices.AddNotice( "block", Color.Red, "Inventory Full", 2 );
	}
}
