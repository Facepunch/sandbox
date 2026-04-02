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

	private static SoundEvent _defaultSound = ResourceLibrary.Get<SoundEvent>( "entities/thruster/sounds/thruster_loop_default.sound" );

	/// <summary>
	/// Looping sound played while the thruster is active.
	/// </summary>
	[Property, Group( "Sound" )]
	public SoundEvent ThrusterSound { get; set; }

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
		StopThrusterSound();
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

		if ( state )
		{
			StartThrusterSound();
			Sandbox.Services.Stats.Increment( "tool.thruster.activate", 1 );
		}
		else
		{
			StopThrusterSound();
		}

		Network.Refresh();
	}

	void StartThrusterSound()
	{
		if ( _thrusterSound.IsValid() && !_thrusterSound.IsStopped ) return;

		var sound = ThrusterSound ?? _defaultSound;
		if ( sound is null ) return;

		_thrusterSound = Sound.Play( sound, WorldPosition );
		_thrusterSound.Parent = GameObject;
		_thrusterSound.FollowParent = true;
	}

	void StopThrusterSound()
	{
		if ( _thrusterSound.IsValid() )
		{
			_thrusterSound.Stop();
			_thrusterSound = default;
		}
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
