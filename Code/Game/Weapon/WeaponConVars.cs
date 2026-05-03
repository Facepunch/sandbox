namespace Sandbox;

/// <summary>
/// Console variables that control weapon behaviour globally.
/// </summary>
public static class WeaponConVars
{
	/// <summary>
	/// When enabled, weapons have unlimited ammo — no ammo is consumed when firing.
	/// </summary>
	[ConVar( "sb.weapon.unlimitedammo", ConVarFlags.Replicated, Help = "When enabled, weapons have unlimited ammo." )]
	public static bool UnlimitedAmmo { get; set; } = false;

	/// <summary>
	/// When enabled, reserve ammo never depletes — clip ammo is still consumed normally, but you can always reload.
	/// </summary>
	[ConVar( "sb.weapon.infinitereserves", ConVarFlags.Replicated, Help = "When enabled, reserve ammo is infinite — clip ammo is still consumed." )]
	public static bool InfiniteReserves { get; set; } = false;
}
