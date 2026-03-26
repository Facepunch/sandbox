using Sandbox.UI;

public sealed partial class GameLimitsSystem
{
	/// <summary>
	/// When false, all limit checks pass without restriction.
	/// </summary>
	[Title( "Limits Enabled" ), Group( "Limits" )]
	[ConVar( "sb.limits.enabled", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	public static bool Enabled { get; set; } = true;

	/// <summary>
	/// Maximum props (including duplications) a single player may have active.
	/// </summary>
	[Title( "Max Props" ), Group( "Limits" )]
	[ConVar( "sb.limits.props", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 256 ), Step( 1 )]
	public static int MaxProps { get; set; } = 128;

	/// <summary>
	/// Maximum scripted entities a single player may have active.
	/// </summary>
	[Title( "Max Entities" ), Group( "Limits" )]
	[ConVar( "sb.limits.entities", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 128 ), Step( 1 )]
	public static int MaxEntities { get; set; } = 32;

	/// <summary>
	/// Maximum thrusters a single player may have active.
	/// </summary>
	[Title( "Max Thrusters" ), Group( "Limits" )]
	[ConVar( "sb.limits.thrusters", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 128 ), Step( 1 )]
	public static int MaxThrusters { get; set; } = 32;

	/// <summary>
	/// Maximum balloons a single player may have active.
	/// </summary>
	[Title( "Max Balloons" ), Group( "Limits" )]
	[ConVar( "sb.limits.balloons", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 128 ), Step( 1 )]
	public static int MaxBalloons { get; set; } = 8;

	/// <summary>
	/// Maximum wheels a single player may have active.
	/// </summary>
	[Title( "Max Wheels" ), Group( "Limits" )]
	[ConVar( "sb.limits.wheels", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 48 ), Step( 1 )]
	public static int MaxWheels { get; set; } = 16;

	/// <summary>
	/// Maximum constraints (ropes, welds, etc.) a single player may have active.
	/// </summary>
	[Title( "Max Constraints" ), Group( "Limits" )]
	[ConVar( "sb.limits.constraints", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 2048 ), Step( 4 )]
	public static int MaxConstraints { get; set; } = 1024;
}
