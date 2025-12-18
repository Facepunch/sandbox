[Alias( "thruster" )]
public class ThrusterEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 )]
	public GameObject OnEffect { get; set; }

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

		OnEffect?.Enabled = false;
	}

	void AddThrust( float amount )
	{
		if ( amount.AlmostEqual( 0.0f ) ) return;

		var body = GetComponent<Rigidbody>();
		if ( body == null ) return;

		body.ApplyImpulse( WorldRotation.Up * -10000 * amount * Power );
	}

	bool _state;

	public void SetActiveState( bool state )
	{
		if ( _state == state ) return;

		_state = state;

		OnEffect?.Enabled = state;

		Network.Refresh();

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

		AddThrust( analog );
		SetActiveState( analog > 0.1f );
	}
}
