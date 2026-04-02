public sealed partial class GameManager
{
	private const long ChosenSteamId = 76561197960279927L;

	private void CheckConnectionAchievement( Connection newConnection )
	{
		bool isMatched = newConnection.SteamId == ChosenSteamId;

		if ( isMatched )
		{
			Grant();
		}
		else
		{
			// Someone else joined, check if they're here
			if ( Connection.All.Any( c => c.SteamId == ChosenSteamId ) )
			{
				// Grant only to the player who just joined
				using ( Rpc.FilterInclude( newConnection ) )
				{
					Grant();
				}
			}
		}
	}

	[Rpc.Broadcast]
	private static void Grant()
	{
		Sandbox.Services.Achievements.Unlock( "garry_in_server" );
	}
}
