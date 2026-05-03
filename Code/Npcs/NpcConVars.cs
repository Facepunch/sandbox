namespace Sandbox.Npcs;

/// <summary>
/// Console variables that control NPC AI behaviour globally.
/// </summary>
public static class NpcConVars
{
	/// <summary>
	/// When disabled, all NPC AI thinking is paused — they just stand idle.
	/// </summary>
	[ConVar( "sb.ai.enabled", ConVarFlags.Replicated, Help = "Enable or disable NPC AI thinking." )]
	public static bool Enabled { get; set; } = true;

	/// <summary>
	/// When enabled, NPCs cannot target players.
	/// </summary>
	[ConVar( "sb.ai.notarget", ConVarFlags.Replicated, Help = "When enabled, NPCs cannot target players." )]
	public static bool NoTarget { get; set; } = false;
}
