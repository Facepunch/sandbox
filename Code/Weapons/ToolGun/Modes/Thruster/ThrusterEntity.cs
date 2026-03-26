[Alias( "thruster" )]
public class ThrusterEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 )]
	public GameObject OnEffect { get; set; }

	[Property, ClientEditable, Range( 0, 1 )]
	public float Power { get; set; } = 0.5f;

	[Property, ClientEditable]
	public bool Invert { get; set; } = false;

	[Property, ClientEditable]
	public bool HideEffects { get; set; } = false;

	/// <summary>
	/// While the client input is active we'll apply thrust
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	/// <summary>
	/// While this input is active we'll apply thrust in the opposite direction
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Reverse { get; set; }

	/// <summary>
	/// Current thrust output, -1 to 1. Updated every control frame.
	/// </summary>
	public float ThrustAmount { get; private set; }

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

		body.ApplyImpulse( WorldRotation.Up * -10000 * amount * Power * (Invert ? -1f : 1f) );
	}

	bool _state;

	public void SetActiveState( bool state )
	{
		if ( _state == state ) return;

		_state = state;

		if ( !HideEffects )
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
		var forward = Activate.GetAnalog();
		var backward = Reverse.GetAnalog();
		var analog = forward - backward;
		ThrustAmount = analog;

		AddThrust( analog );
		SetActiveState( MathF.Abs( analog ) > 0.1f );
	}
}
