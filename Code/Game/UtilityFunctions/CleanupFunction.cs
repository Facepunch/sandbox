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
		Cleanup( Rpc.Caller.SteamId );
		Notices.SendNotice( Rpc.Caller, "cleaning_services", Color.Green, "Cleaned up your objects" );
	}

	internal static void Cleanup( ulong steamId )
	{
		var removable = Game.ActiveScene.GetAllComponents<Ownable>()
			.Where( o => o.OwnerSteamId == steamId )
			.ToArray();

		foreach ( var ownable in removable )
			ownable.GameObject.Destroy();
	}

	[Rpc.Host]
	internal static void CleanUpAll()
	{
		if ( !Rpc.Caller.IsHost ) return;

		CleanupSystem.Current.Cleanup();
	}
}
