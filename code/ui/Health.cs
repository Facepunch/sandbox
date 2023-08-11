using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;

public class health : Panel
{
	public Label Label;

	public health()
	{
		Label = Add.Label( "100", "value" );
	}

	public override void Tick()
	{
		var player = Game.LocalPawn;
		if ( player == null ) return;

		Label.Text = $"{player.Health.CeilToInt()}";
	}
}
