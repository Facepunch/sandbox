using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System.Timers;
public enum jobType {
citizen = 0,
thief = 1,
gundealer = 2,
hobo = 3,
hitman = 4,
ganglead = 5,
police = 6,

}
public partial class Health : Panel
{
	public Label Label;

	public static string Job = "Pick Job";
	public static int Money = 0;
	public Health()
	{
		Label = Add.Label( "Health", "value" );

	}

	public override void Tick()
	{
		var player = Game.LocalPawn;
		if ( player == null ) return;

		Label.Text = $"HP: {player.Health.CeilToInt()} | Job: {Job} | ${Money}";


	}
	[ConCmd.Client( "myjob" )]
	public static void MyJob()
	{
		Log.Info( $"Hello I am {Job}" );
	}
	[ClientRpc]
	public static void whatever(jobType recieveJob, long steamId)
	{
		if (Game.LocalClient.SteamId  == steamId) 
		{
			Log.Info(recieveJob.ToString());
		}
	
		
	}
	[ConCmd.Server( "ServerSetJob" )]
	public static void ServerSetJob(jobType serverJob, long steamId)
	{
		whatever(serverJob, steamId);
		Log.Error($"{ConsoleSystem.Caller} has switched job to " + serverJob.ToString());
	}
}

