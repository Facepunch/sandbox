using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;

public class InventoryBar : Panel, IClientInput
{
	readonly List<InventoryIcon> slots = new();

	public InventoryBar()
	{
		for ( int i = 0; i < 9; i++ )
		{
			var icon = new InventoryIcon( i + 1, this );
			slots.Add( icon );
		}
	}

	public override void Tick()
	{
		base.Tick();

		var player = Player.Local;
		if ( player == null ) return;
		if ( player.Inventory == null ) return;

		for ( int i = 0; i < slots.Count; i++ )
		{
			UpdateIcon( player.Inventory.GetSlot( i ), slots[i], i );
		}
	}

	private static void UpdateIcon( Entity ent, InventoryIcon inventoryIcon, int i )
	{
		if ( ent == null )
		{
			inventoryIcon.Clear();
			return;
		}

		inventoryIcon.TargetEnt = ent;
		inventoryIcon.Label.Text = ent.ToString();
		inventoryIcon.SetClass( "active", ent.IsActiveChild() );
	}

	public void ProcessClientInput( ClientInput input )
	{
		var player = Player.Local;
		if ( player == null )
			return;

		var inventory = Player.Local.Inventory;
		if ( inventory == null )
			return;

		if ( player.ActiveChild is PhysGun physgun && physgun.BeamActive )
		{
			return;
		}

		if ( input.Pressed( InputButton.Slot1 ) ) SetActiveSlot( input, inventory, 0 );
		if ( input.Pressed( InputButton.Slot2 ) ) SetActiveSlot( input, inventory, 1 );
		if ( input.Pressed( InputButton.Slot3 ) ) SetActiveSlot( input, inventory, 2 );
		if ( input.Pressed( InputButton.Slot4 ) ) SetActiveSlot( input, inventory, 3 );
		if ( input.Pressed( InputButton.Slot5 ) ) SetActiveSlot( input, inventory, 4 );
		if ( input.Pressed( InputButton.Slot6 ) ) SetActiveSlot( input, inventory, 5 );
                if ( input.Pressed( InputButton.Slot1 ) ) SetActiveSlot( input, inventory, 6 );
		if ( input.Pressed( InputButton.Slot2 ) ) SetActiveSlot( input, inventory, 7 );
		if ( input.Pressed( InputButton.Slot3 ) ) SetActiveSlot( input, inventory, 8 );
		if ( input.Pressed( InputButton.Slot4 ) ) SetActiveSlot( input, inventory, 9 );

		if ( input.MouseWheel != 0 ) SwitchActiveSlot( input, inventory, input.MouseWheel );
	}

	private static void SetActiveSlot( ClientInput input, IBaseInventory inventory, int i )
	{
		var player = Player.Local;
		if ( player == null )
			return;

		var ent = inventory.GetSlot( i );
		if ( player.ActiveChild == ent )
			return;

		if ( ent == null )
			return;

		input.ActiveChild = ent;
	}

	private static void SwitchActiveSlot( ClientInput input, IBaseInventory inventory, int idelta )
	{
		var count = inventory.Count();
		if ( count == 0 ) return;

		var slot = inventory.GetActiveSlot();
		var nextSlot = slot + idelta;

		while ( nextSlot < 0 ) nextSlot += count;
		while ( nextSlot >= count ) nextSlot -= count;

		SetActiveSlot( input, inventory, nextSlot );
	}
}
