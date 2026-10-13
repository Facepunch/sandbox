using Editor.Mcp;

namespace Sandbox.Bots;

[McpToolset( "bots", "Bot players that spawn, shoot, build and break things" )]
public static class BotTools
{
	/// <summary>
	/// Set how many bots the host runs. Every bot, including ones from bot_add, plays like a chaotic sandbox player.
	/// Host only.
	/// </summary>
	[McpTool( "bot_count" )]
	public static object Bots( [Description( "How many bots to have" )] int count )
	{
		BotSystem.Current?.SetBotCount( count );
		return Bot.All.Select( x => x.Name ).ToArray();
	}

	/// <summary>
	/// Let a bot brain drive this instance's own player, or hand it back. Use it on clients, which can't add bots.
	/// </summary>
	[McpTool( "bot_local" )]
	public static void Local( bool enabled )
	{
		if ( BotSystem.Current is not { } system )
			throw new InvalidOperationException( "No game is running yet" );

		system.SetLocal( enabled );
	}

	/// <summary>
	/// Change how often bots do something. Actions: wander, noclip, spawn_prop, spawn_prop_pile, spawn_entity,
	/// spawn_npc, spawn_dupe, shoot, tool_constraint, tool_other, duplicate, physgun, gravgun, use, undo,
	/// suicide, flashlight. 0 turns it off.
	/// </summary>
	[McpTool( "bot_weight" )]
	public static Dictionary<string, float> Weight( string action, float weight )
	{
		BotSystem.Weights[action] = weight;
		return BotSystem.Weights;
	}

	/// <summary>
	/// What each bot is doing. Read errors with read_console.
	/// </summary>
	[McpTool.ReadOnly( "bot_status" )]
	public static string[] Status() => BotSystem.Current?.Brains.Select( b => $"{b.Name}: {b.CurrentAction}" ).ToArray() ?? [];
}
