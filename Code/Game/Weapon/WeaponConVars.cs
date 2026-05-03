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
}
