using Sandbox;
using Sandbox.UI;

/// <summary>
/// RPC helper methods for the Cleanup utility page.
/// </summary>
public static class CleanupFunction
{
	[Rpc.Host]
	internal static void CleanUpMine()
	{
		var caller = Rpc.Caller;
		Cleanup( caller );
	}

	[Rpc.Host]
	internal static void CleanUpAll()
	{
		if ( !Rpc.Caller.IsHost ) return;

		CleanupSystem.Current.Cleanup();
	}

	[Rpc.Host]
	internal static void CleanUpTarget( Connection target )
	{
		if ( !Rpc.Caller.IsHost ) return;

		Cleanup( target );
	}

	internal static void Cleanup( Connection caller )
	{
		Assert.True( Networking.IsHost, "Only the host may call this method!" );

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
}
