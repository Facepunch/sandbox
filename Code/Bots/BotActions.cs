namespace Sandbox.Bots;

/// <summary>
/// Runs a bot's menu actions from its own player's update, so they happen as the bot.
/// Only exists on the host.
/// </summary>
public sealed class BotActions : Component
{
	public Queue<Action> Pending { get; } = new();

	protected override void OnUpdate()
	{
		while ( Pending.TryDequeue( out var action ) )
		{
			try { action(); }
			catch ( Exception e ) { Log.Warning( $"[bot] action: {e}" ); }
		}
	}
}
