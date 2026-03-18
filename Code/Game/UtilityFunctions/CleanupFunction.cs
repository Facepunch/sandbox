using Sandbox;
using Sandbox.UI;

/// <summary>
/// Opens cleanup pages in the utility tab right panel.
/// </summary>
[Icon( "🧹" )]
[Title( "Cleanup" )]
[Group( "World" )]
[Order( 0 )]
public class CleanupFunction : UtilityFunction
{
	[Rpc.Host]
	internal static void CleanUpMine()
	{
		var caller = Rpc.Caller;

		var removable = Game.ActiveScene.GetAllComponents<Ownable>()
			.Where( o => o.Owner == caller );

		var count = 0;
		foreach ( var ownable in removable.ToArray() )
		{
			ownable.GameObject.Destroy();
			count++;
		}

		Notices.SendNotice( caller, "cleaning_services", Color.Green, $"Cleaned up {count} objects" );
	}

	[Rpc.Host]
	internal static void CleanUpAll()
	{
		if ( !Rpc.Caller.IsHost ) return;

		CleanupSystem.Current.Cleanup();
	}
}
