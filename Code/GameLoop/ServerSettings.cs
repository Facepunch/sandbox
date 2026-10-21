/// <summary>
/// Server-controlled settings replicated to all clients.
/// </summary>
internal static class ServerSettings
{
	/// <summary>
	/// When enabled, developer entities are visible in the spawn menu for all players.
	/// Always on in the editor regardless of this value.
	/// </summary>
	[Title( "Show Developer Entities" )]
	[Group( "Other" )]
	[ConVar( "sandbox.developer.entities", ConVarFlags.Replicated | ConVarFlags.GameSetting | ConVarFlags.Server, Help = "Show developer entities in the spawn menu." )]
	public static bool DeveloperEntities { get; set; } = false;

	/// <summary>
	/// On respawn, players get back the weapons they were carrying instead of the starter items.
	/// </summary>
	[Title( "Re-equip Last" )]
	[Group( "Players" )]
	[ConVar( "sandbox.reequiplast", ConVarFlags.Replicated | ConVarFlags.GameSetting | ConVarFlags.Server, Help = "Give players back the weapons they were carrying when they respawn." )]
	public static bool ReEquipLast { get; set; } = false;

	/// <summary>
	/// Players can edit and resize the bones of other players. When disabled, admins can still do it, and other players are restricted to edit only their own body.
	/// </summary>
	[Title( "Edit Other Players Bones" )]
	[Group( "Players" )]
	[ConVar( "sandbox.bones.otherplayers", ConVarFlags.Replicated | ConVarFlags.GameSetting | ConVarFlags.Server, Help = "Let players edit and resize the bones of other players. When off, only admins can." )]
	public static bool EditOtherPlayersBones { get; set; } = true;

	/// <summary>
	/// Returns true if developer entities should be shown.
	/// </summary>
	public static bool ShowDeveloperEntities => Game.IsEditor || DeveloperEntities;
}
