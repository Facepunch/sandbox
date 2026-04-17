[Alias( "hoverball" )]
public class HoverballEntity : Component, IPlayerControllable
{
	/// <summary>
	/// Is the hoverball on?
	/// </summary>
	[Property, Sync, ClientEditable]
	public bool IsEnabled { get; private set; } = true;

	/// <summary>
	/// The world Z position the hoverball is trying to maintain.
	/// </summary>
	[Property, Sync]
	public float TargetZ { get; private set; }

	/// <summary>
	/// How fast the target height changes when inputs are held.
	/// </summary>
	[Property, Sync, ClientEditable, Range( 0, 20 )]
	public float Speed { get; set; } = 1f;

	/// <summary>
	/// Horizontal air resistance applied while hovering. Also increases vertical damping.
	/// </summary>
	[Property, Sync, ClientEditable, Range( 0, 10 )]
	public float AirResistance { get; set; } = 0f;

	/// <summary>
	/// While held, raises the hover target.
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Up { get; set; }

	/// <summary>
	/// While held, lowers the hover target.
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Down { get; set; }

	/// <summary>
	/// Toggles the hoverball on/off
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Toggle { get; set; }

	[Property]
	public GameObject OnEffect { get; set; }

	[Property] public SoundEvent EnableSound { get; set; }
	[Property] public SoundEvent DisableSound { get; set; }

	private float _zVelocity;
	private bool _toggleWasHeld;

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		TargetZ = WorldPosition.z;

		var rb = GetComponent<Rigidbody>();
		if ( rb.IsValid() )
			rb.Gravity = !IsEnabled;
	}

	protected override void OnUpdate()
	{
		if ( OnEffect.IsValid() )
			OnEffect.Enabled = IsEnabled;
	}

	public void OnStartControl() { }

	public void OnEndControl()
	{
		_zVelocity = 0f;
	}

	public void OnControl()
	{
		var toggleHeld = Toggle.GetAnalog() > 0.5f;
		if ( toggleHeld && !_toggleWasHeld )
		{
			DoToggle();
		}

		_toggleWasHeld = toggleHeld;

		// Accumulate velocity
		var upAnalog = Up.GetAnalog();
		var downAnalog = Down.GetAnalog();
		var zDir = upAnalog - downAnalog;
		_zVelocity = zDir != 0f ? zDir * Time.Delta * 5000f : 0f;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		var rb = GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		if ( !IsEnabled ) return;

		// Shift target height from held inputs
		if ( _zVelocity != 0f )
		{
			TargetZ += _zVelocity * Time.Delta * Speed;
		}

		var pos = WorldPosition;
		var vel = rb.Velocity;
		var distance = TargetZ - pos.z;

		// Drive Z velocity toward a target proportional to distance
		var targetVelZ = Math.Clamp( distance * 20f, -400f, 400f );
		var newVelZ = vel.z + (targetVelZ - vel.z) * Math.Min( Time.Delta * 15f * (AirResistance + 1f), 1f );

		var newVel = vel.WithZ( newVelZ );

		// Horizontal air resistance
		if ( AirResistance > 0f )
		{
			var drag = Math.Min( AirResistance * Time.Delta * 5f, 1f );
			newVel = newVel.WithX( vel.x * (1f - drag) ).WithY( vel.y * (1f - drag) );
		}

		rb.Velocity = newVel;
	}

	private void DoToggle()
	{
		IsEnabled = !IsEnabled;

		if ( IsEnabled )
			Sound.Play( EnableSound, WorldPosition );
		else
			Sound.Play( DisableSound, WorldPosition );

		var rb = GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		if ( IsEnabled )
		{
			TargetZ = WorldPosition.z;
			rb.Gravity = false;
		}
		else
		{
			rb.Gravity = true;
		}
	}
}
