/// <summary>
/// Defines a type of ammo that can be shared across weapons.
/// Weapons that reference the same AmmoResource draw from the same reserve pool on the player.
/// </summary>
[AssetType( Name = "Ammo Type", Extension = "ammo", Category = "Sandbox" )]
public class AmmoResource : GameResource
{
	/// <summary>
	/// Display name for this ammo type.
	/// </summary>
	[Property] public string Title { get; set; }

	/// <summary>
	/// Optional icon displayed in HUD and inventory.
	/// </summary>
	[Property] public Texture Icon { get; set; }

	/// <summary>
	/// Maximum reserve ammo a player can hold for this type.
	/// </summary>
	[Property] public int MaxReserve { get; set; } = 120;

	/// <summary>
	/// How much reserve ammo is seeded into the shared pool the first time
	/// a weapon of this ammo type is picked up (i.e. when the pool doesn't
	/// exist yet on the player).
	/// </summary>
	[Property] public int DefaultStartingAmmo { get; set; } = 0;
}
