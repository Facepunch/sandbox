namespace Sandbox;

public sealed class EditableWheel : Component
{

	[ClientEditable]
	[Range( -500, 500 )]
	public float WheelSpeed { get; set; } = 120f;


	[ClientInput]
	public void WheelForward()
	{
		Log.Info( "Forward" );
	}

}
