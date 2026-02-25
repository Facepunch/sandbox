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

		var removable = Game.ActiveScene.GetAllObjects( true )
			.Where( go => go.Tags.Contains( "removable" ) );

		var count = 0;
		foreach ( var go in removable.ToArray() )
		{
			go.Destroy();
			count++;
		}

		Notices.SendNotice( Rpc.Caller, "cleaning_services", Color.Green, $"Cleaned up {count} objects" );
	}
}
