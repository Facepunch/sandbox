using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System.Timers;

public class Health : Panel
{
	public Label Label;
	public static string Job;
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
}
