/// <summary>
/// The game settings for this server
/// </summary>
public static class GameSettings
{
	/// <summary>
	/// Give you everything
	/// </summary>
	[ConVar( "sbdm.cheatmode", ConVarFlags.Replicated | ConVarFlags.Saved | ConVarFlags.GameSetting | ConVarFlags.Cheat ), Group( "Cheats" ), Title( "Spawn with all weapons" )]
	public static bool CheatMode { get; set; } = false;

	/// <summary>
	/// All weapons have infinite ammo
	/// </summary>
	[ConVar( "sbdm.infammo", ConVarFlags.Replicated | ConVarFlags.GameSetting ), Group( "Cheats" )]
	public static bool InfiniteAmmo { get; set; } = false;

	/// <summary>
	/// Multiply all bullet radiuses by this
	/// </summary>
	[ConVar( "skill.bulletradius", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0.1f, 10f, 0.1f ), Group( "Weapons" )]
	public static float BulletRadius { get; set; } = 1;

	/// <summary>
	/// Radius of the mp5's bullet
	/// </summary>
	[ConVar( "mp5.bulletradius", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0.1f, 10f, 0.1f ), Group( "Weapons" ), Title( "MP5 Bullet Radius" )]
	public static float Mp5BulletRadius { get; set; } = 1;

	/// <summary>
	/// Radius of the glock's bullet
	/// </summary>
	[ConVar( "glock.bulletradius", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0.1f, 10f, 0.1f ), Group( "Weapons" )]
	public static float GlockBulletRadius { get; set; } = 1f;

	/// <summary>
	/// Radius of the python's bullet
	/// </summary>
	[ConVar( "python.bulletradius", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0.1f, 10f, 0.1f ), Group( "Weapons" )]
	public static float PythonBulletRadius { get; set; } = 2f;

	/// <summary>
	/// Radius of the shotgun's bullet
	/// </summary>
	[ConVar( "shotgun.bulletradius", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0.1f, 10f, 0.1f ), Group( "Weapons" )]
	public static float ShotgunBulletRadius { get; set; } = 2f;

	/// <summary>
	/// Radius of the crossbow's bullet (when zoomed)
	/// </summary>
	[ConVar( "crossbow.bulletradius", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0.1f, 10f, 0.1f ), Group( "Weapons" )]
	public static float CrossbowBulletRadius { get; set; } = 1f;

	/// <summary>
	/// Minimum time between throwing satchels
	/// </summary>
	[ConVar( "satchel.thowdelay", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0.1f, 10f, 0.1f ), Group( "Weapons" )]
	public static float SatchelThrowDelay { get; set; } = 0.2f;

	/// <summary>
	/// Minimum time between throwing satchels
	/// </summary>
	[ConVar( "satchel.thowpower", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 250f, 2500f, 1f ), Group( "Weapons" )]
	public static float SatchelThrowPower { get; set; } = 500.0f;

	/// <summary>
	/// Scale of damage when headshotting a player with most weapons.
	/// </summary>
	[ConVar( "sbdm.headshotscale", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0.1f, 10f, 0.1f ), Group( "Weapons" )]
	public static float HeadshotDamageScale { get; set; } = 2.0f;

	/// <summary>
	/// Maximum decals that can exist. Remove old ones when new ones are created.
	/// </summary>
	[ConVar( "sbdm.maxdecals", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 64, 1024f, 1f ), Group( "Weapons" )]
	public static int MaxDecals { get; set; } = 512;

	/// <summary>
	/// How much to reduce damage to ourselves
	/// </summary>
	[ConVar( "sbdm.selfdamagescale", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 1, 0.05f )]
	public static float SelfDamageScale { get; set; } = 0.25f;

}
