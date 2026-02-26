using Sandbox;
using Sandbox.UI;

/// <summary>
/// Cleans up all props and entities spawned by the local player.
/// </summary>
[Icon( "🧼" )]
[Title( "Clean Up" )]
[Group( "User" )]
[Order( 0 )]
public class UserCleanupFunction : UtilityFunction
{
	public override void Execute()
	{
		CleanUp();
	}

	[Rpc.Host]
	private static void CleanUp()
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
}
