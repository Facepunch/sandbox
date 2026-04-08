/// <summary>
/// Server-controlled settings replicated to all clients.
/// </summary>
public static class ServerSettings
{
	/// <summary>
	/// When enabled, developer entities are visible in the spawn menu for all players.
	/// Always on in the editor regardless of this value.
	/// </summary>
	[Title( "Show Developer Entities" )]
	[Group( "Other" )]
	[ConVar( "sb.developer_entities", ConVarFlags.Replicated | ConVarFlags.GameSetting | ConVarFlags.Server, Help = "Show developer entities in the spawn menu." )]
	public static bool DeveloperEntities { get; set; } = false;

	/// <summary>
	/// Returns true if developer entities should be shown.
	/// </summary>
	public static bool ShowDeveloperEntities => Game.IsEditor || DeveloperEntities;
}
