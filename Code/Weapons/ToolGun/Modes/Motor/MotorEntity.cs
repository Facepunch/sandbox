
/// <summary>
/// Drives a <see cref="HingeJoint"/> at a continuous angular velocity — a spinning axle,
/// turntable, or wheel that isn't a ground-contact wheel.
/// </summary>
public sealed class MotorEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 ), ClientEditable]
	public float Speed { get; set; } = 0.5f;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Torque { get; set; } = 0.5f;

	[Property, ClientEditable]
	public bool Reversed { get; set; }

	/// <summary>
	/// Spins on its own as soon as it's created, without needing a driver or a wire signal.
	/// </summary>
	[Property, ClientEditable]
	public bool StartActive { get; set; } = true;

	[Property, ClientEditable, ToggleGroup( "Limited" )]
	public bool Limited { get; set; }

	[Property, ClientEditable, ToggleGroup( "Limited" ), Range( -180, 180 )]
	public float MinAngle { get; set; } = -90f;

	[Property, ClientEditable, ToggleGroup( "Limited" ), Range( -180, 180 )]
	public float MaxAngle { get; set; } = 90f;

	/// <summary>
	/// While held, spins forward regardless of <see cref="Reversed"/> or <see cref="StartActive"/>.
	/// </summary>
	[Property, ClientEditable]
	public ClientInput Forward { get; set; }

	/// <summary>
	/// While held, spins backward regardless of <see cref="Reversed"/> or <see cref="StartActive"/>.
	/// </summary>
	[Property, ClientEditable]
	public ClientInput Reverse { get; set; }

	[Property, ClientEditable]
	public ClientInput Toggle { get; set; }

	[Property]
	public HingeJoint Joint { get; set; }

	/// <summary>
	/// Emits the joint's current angle, normalized so a full turn is 1.0 — wire it into a
	/// gauge, or into another motor's Forward/Reverse to sync two axles.
	/// </summary>
	[SignalOutput]
	public SignalOutput AngleOutput { get; set; } = new();

	const float MaxDegreesPerSecond = 720f;
	const float MaxTorqueScale = 200000f;

	bool _active;
	float _lastEmitted = float.NaN;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		_active = StartActive;
	}

	[SignalInput( Id = nameof( Forward ), Default = true )]
	public void ForwardSignal( float amount ) => Drive( amount, 0f );

	[SignalInput( Id = nameof( Reverse ) )]
	public void ReverseSignal( float amount ) => Drive( 0f, amount );

	[SignalInput( Id = nameof( Toggle ) )]
	public void ToggleSignal() => _active = !_active;

	public void OnControl()
	{
		if ( Toggle.Pressed() ) _active = !_active;

		Drive( Forward.GetAnalog(), Reverse.GetAnalog() );
	}

	void Drive( float forward, float reverse )
	{
		if ( !Networking.IsHost ) return;
		if ( !Joint.IsValid() ) return;

		Joint.MinAngle = Limited ? MinAngle : -100000f;
		Joint.MaxAngle = Limited ? MaxAngle : 100000f;

		var held = forward > 0.5f || reverse > 0.5f;

		if ( !held && !_active )
		{
			Joint.Motor = HingeJoint.MotorMode.Disabled;
			return;
		}

		var direction = held ? (forward > reverse ? 1f : -1f) : (Reversed ? -1f : 1f);

		Joint.Motor = HingeJoint.MotorMode.TargetVelocity;
		Joint.TargetVelocity = direction * Speed * MaxDegreesPerSecond;
		Joint.MaxTorque = Torque * MaxTorqueScale;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !Joint.IsValid() ) return;

		var turns = Joint.Angle / 360f;
		if ( float.IsNaN( _lastEmitted ) || MathF.Abs( turns - _lastEmitted ) > 0.001f )
		{
			_lastEmitted = turns;
			AngleOutput.Emit( this, turns );
		}
	}
}
