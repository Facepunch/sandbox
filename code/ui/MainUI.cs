using System.ComponentModel;
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System.Timers;
using System.Linq;

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
	public static void SendJob(Job recieveJob, long steamId)
	{
		if (Game.LocalClient.SteamId  == steamId) 
		{
			Log.Info(recieveJob.id);
		}
	
		
	}
	[ConCmd.Server( "ServerSetJob" )]
	public static void ServerSetJob(Job serverJob, long steamId)
	{
		SendJob(serverJob, steamId);
		Log.Error($"{ConsoleSystem.Caller} has switched job to " + serverJob.id);

	}
}

