namespace Facepunch;

public sealed class Door : Component, Component.IPressable
{
	/// <summary>
	/// Animation curve to use, X is the time between 0-1 and Y is how much the door is open to its target angle from 0-1.
	/// </summary>
	[Property] public Curve AnimationCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1.0f ) );

	/// <summary>
	/// Sound to play when a door is opened.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent OpenSound { get; set; }

	/// <summary>
	/// Sound to play when a door is fully opened.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent OpenFinishedSound { get; set; }

	/// <summary>
	/// Sound to play when a door is closed.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent CloseSound { get; set; }

	/// <summary>
	/// Sound to play when a door has finished closing.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent CloseFinishedSound { get; set; }

	/// <summary>
	/// Optional pivot point, origin will be used if not specified.
	/// </summary>
	[Property] public GameObject Pivot { get; set; }

	/// <summary>
	/// How far should the door rotate.
	/// </summary>
	[Property, Range( 0.0f, 90.0f )] public float TargetAngle { get; set; } = 90.0f;

	/// <summary>
	/// How long in seconds should it take to open this door.
	/// </summary>
	[Property] public float OpenTime { get; set; } = 0.5f;

	/// <summary>
	/// Open away from the person who uses this door.
	/// </summary>
	[Property] public bool OpenAwayFromPlayer { get; set; } = true;

	/// <summary>
	/// The door's state
	/// </summary>
	public enum DoorState
	{
		Open,
		Opening,
		Closing,
		Closed
	}

	Transform _startTransform;
	Vector3 _pivotPosition;
	bool _reverseDirection;

	[Sync] private TimeSince LastUse { get; set; }
	[Sync] private DoorState _state { get; set; }

	/// <summary>
	/// Called when the door's state changes. When it opens, closed, is closing, etc..
	/// </summary>
	[Property, Group( "Events" )]
	public Action<DoorState> OnStateChanged { get; set; }
	
	public DoorState State
	{
		get => _state; 
		set
		{
			if ( _state == value )
				return;

			_state = value;
			OnDoorStateChanged( value );
		}
	}

	void OnDoorStateChanged( DoorState value )
	{
		OnStateChanged?.Invoke( value );

		if ( IsProxy )
			return;

		if ( value == DoorState.Open )
		{
			if ( OpenFinishedSound is not null )
				PlaySound( OpenFinishedSound );
		}
		else if ( value == DoorState.Closed )
		{
			if ( CloseFinishedSound is not null )
				PlaySound( CloseFinishedSound );
		}
	}

	protected override void OnStart()
	{
		_startTransform = Transform.Local;
		_pivotPosition = Pivot is not null ? Pivot.WorldPosition : _startTransform.Position;
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		return State is DoorState.Open or DoorState.Closed;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		Toggle( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	public void Toggle( GameObject presser )
	{
		LastUse = 0;

		if ( State == DoorState.Closed )
		{
			State = DoorState.Opening;
			if ( OpenSound is not null )
				PlaySound( OpenSound );

			if ( OpenAwayFromPlayer )
			{
				var doorToPlayer = (presser.WorldPosition - _pivotPosition).Normal;
				var doorForward = Transform.Local.Rotation.Forward;

				_reverseDirection = Vector3.Dot( doorToPlayer, doorForward ) > 0;
			}
		}
		else if ( State == DoorState.Open )
		{
			State = DoorState.Closing;
			if ( CloseSound is not null )
				PlaySound( CloseSound );
		}
	}

	[Rpc.Broadcast]
	private void PlaySound( SoundEvent sound )
	{
		GameObject.PlaySound( sound );
	}

	protected override void OnFixedUpdate()
	{
		// Don't do anything if we're not opening or closing
		if ( State != DoorState.Opening && State != DoorState.Closing )
			return;

		// Normalize the last use time to the amount of time to open
		var time = LastUse.Relative.Remap( 0.0f, OpenTime, 0.0f, 1.0f );

		// Evaluate our animation curve
		var curve = AnimationCurve.Evaluate( time );

		// Rotate backwards if we're closing
		if ( State == DoorState.Closing ) curve = 1.0f - curve;

		var targetAngle = TargetAngle;
		if ( _reverseDirection ) targetAngle *= -1.0f;

		// Do the rotation
		Transform.Local = _startTransform.RotateAround( _pivotPosition, Rotation.FromYaw( curve * targetAngle ) );

		// If we're done finalize the state and play the sound
		if ( time < 1f ) return;

		State = State == DoorState.Opening ? DoorState.Open : DoorState.Closed;
	}
}
