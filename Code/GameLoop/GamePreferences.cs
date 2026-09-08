/// <summary>
/// The local user's preferences in Deathmatch
/// </summary>
internal static class GamePreferences
{
	/// <summary>
	/// Enables automatic switching to better weapons on item pickup
	/// </summary>
	[ConVar( "sandbox.autoswitch", ConVarFlags.UserInfo | ConVarFlags.Saved )]
	public static bool AutoSwitch { get; set; } = true;

	/// <summary>
	/// Enables movement bob, weapon inertia and animation-driven camera motion.
	/// </summary>
	[ConVar( "sandbox.viewbob", ConVarFlags.Saved )]
	[Group( "Camera" )]
	public static bool ViewBobbing { get; set; } = true;

	/// <summary>
	/// Strength of the camera tilt when moving sideways
	/// </summary>
	[ConVar( "sandbox.viewtilt", ConVarFlags.Saved, Help = "Sideways camera tilt strength. 0 disables tilt, 1 restores the original strength." )]
	[Range( 0f, 1f ), Step( 0.05f ), Group( "Camera" )]
	public static float ViewTilt { get; set; } = 0.35f;

	/// <summary>
	/// Intensity of your camera's screenshake
	/// </summary>
	[ConVar( "sandbox.screenshake", ConVarFlags.Saved )]
	[Range( 0.1f, 2f ), Step( 0.1f ), Group( "Camera" )]
	public static float Screenshake { get; set; } = 0.3f;
}
