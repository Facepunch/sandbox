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
	/// The fallback sound for all thrusters.
	/// </summary>
	private static SoundDefinition _defaultSound => ResourceLibrary.Get<SoundDefinition>( "entities/thruster/sounds/thruster_basic.sndef" );

	/// <summary>
	/// Looping sound played while the thruster is active.
	/// </summary>
	[Property, ClientEditable, Metadata( SoundDefinition.Thruster ), Group( "Sound" )]
	public SoundDefinition ThrusterSound { get; set; }

	/// <summary>
	/// Current thrust output, -1 to 1. Updated every control frame.
	/// </summary>
	public float ThrustAmount { get; private set; }

	private SoundHandle _thrusterSound;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		OnEffect?.Enabled = false;
	}

	protected override void OnDisabled()
	{
		_state = false;
		StopThrusterSound();
	}

	protected override void OnUpdate()
	{
		if ( _state )
		{
			if ( !_thrusterSound.IsValid() )
				StartThrusterSound();
		}
		else
		{
			if ( _thrusterSound.IsValid() )
				StopThrusterSound();
		}
	}

	void AddThrust( float amount )
	{
		if ( amount.AlmostEqual( 0.0f ) ) return;

		var body = GetComponent<Rigidbody>();
		if ( body == null ) return;

		body.ApplyImpulse( WorldRotation.Up * -10000 * amount * Power * (Invert ? -1f : 1f) );
	}

	bool _state;

	[Rpc.Broadcast]
	public void SetActiveState( bool state )
	{
		if ( _state == state ) return;

		_state = state;

		if ( !HideEffects )
			OnEffect?.Enabled = state;
	}

	private void StartThrusterSound()
	{
		if ( _thrusterSound.IsValid() )
			StopThrusterSound();

		var sound = ThrusterSound ?? _defaultSound;
		if ( sound is null ) return;

		_thrusterSound = sound.Play( WorldPosition, GameObject );
	}

	private void StopThrusterSound()
	{
		if ( _thrusterSound.IsValid() )
		{
			_thrusterSound.Stop( 0.5f );
			_thrusterSound = default;
		}
	}

	public void OnControl()
	{
		if ( !Networking.IsHost ) return;

		var forward = Activate.GetAnalog();
		var backward = Reverse.GetAnalog();
		var analog = forward - backward;
		ThrustAmount = analog;

		AddThrust( analog );

		var active = MathF.Abs( analog ) > 0.1f;

		if ( active != _state )
		{
			if ( active )
				Sandbox.Services.Stats.Increment( "tool.thruster.activate", 1 );

			SetActiveState( active );
		}
	}
}
