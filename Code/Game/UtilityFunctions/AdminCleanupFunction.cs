using Sandbox;
using Sandbox.UI;

/// <summary>
/// Cleans up all spawned props and entities from every player. Admin only.
/// </summary>
[Icon( "🧹" )]
[Title( "Clean Up All" )]
[Group( "Admin" )]
[Order( 100 )]
public class AdminCleanupFunction : UtilityFunction
{
	public override void Execute()
	{
		CleanUpAll();
	}

	//
	// TODO: admin??
	//

	public override bool IsVisible()
	{
		return Connection.Local.IsHost;
	}

	[Rpc.Host]
	private static void CleanUpAll()
	{
		if ( !Rpc.Caller.IsHost ) return;

		CleanupSystem.Current.Cleanup();
	}
}
