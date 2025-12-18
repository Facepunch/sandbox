public class WheelEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 )]
	public float Power { get; set; } = 0.5f;

	/// <summary>
	/// While the client input is active we'll apply thrust
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();
	}

	public void OnStartControl()
	{
	}

	public void OnEndControl()
	{
	}

	public void OnControl()
	{
		var analog = Activate.GetAnalog();
	}
}

