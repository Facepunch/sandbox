using static VehicleMath;

/// <summary>
/// What the engine's doing, as far as its sound goes. Each state plays its own recording.
/// </summary>
public enum VehicleSoundState
{
	None,
	Shutdown,
	StartIdle,
	Idle,
	Gear0,
	Gear1,
	Gear2,
	Gear3,
	Gear4,
	Slowdown,
	SlowdownHighSpeed,
	Gear0Resume,
	Gear1Resume,
	Gear2Resume,
	Gear3Resume,
	Gear4Resume,
	Turbo,
	Reverse,
}

/// <summary>
/// A band of speed (as a fraction of top speed) one gear sound covers.
/// </summary>
public sealed class VehicleSoundGear
{
	[Property] public float MaxSpeed { get; set; }
	[Property] public float SpeedApproachFactor { get; set; } = 1;
}

/// <summary>
/// The recording for a sound state.
/// </summary>
public sealed class VehicleStateSound
{
	[Property] public VehicleSoundState State { get; set; }

	/// <summary>
	/// Recording for this state. Left empty, the state is silent.
	/// </summary>
	[Property] public SoundEvent Sound { get; set; }

	/// <summary>
	/// Seconds to stay in this state before moving on.
	/// </summary>
	[Property] public float MinTime { get; set; }
}

/// <summary>
/// A sound for losing a lot of speed at once. Speeds are inches per second.
/// </summary>
public sealed class VehicleCrashSound
{
	[Property] public float MinSpeed { get; set; }
	[Property] public float MinSpeedChange { get; set; }
	[Property] public SoundEvent Sound { get; set; }

	/// <summary>
	/// Only used up to this sound gear (0 for any).
	/// </summary>
	[Property] public int GearLimit { get; set; }
}

/// <summary>
/// Engine, gear, skid and crash sounds for a <see cref="FourWheelVehicle"/>: one recording per sound state, chosen
/// from the throttle and how fast the car is going. And the exhaust backfires - a crackle when you lift off from high
/// revs, pops against the rev limiter and on hard upshifts - with a flame out of the pipe.
///
/// Runs on everyone from the engine state the host syncs on the vehicle.
/// </summary>
[Title( "Vehicle Sounds" ), Category( "Vehicles" ), Icon( "volume_up" )]
public sealed class VehicleSounds : Component
{
	[Property] public FourWheelVehicle Vehicle { get; set; }

	/// <summary>
	/// Where the exhaust comes out, pointing out of the pipe. Backfire flames come from here.
	/// </summary>
	[Property, Group( "Exhaust" )] public GameObject Exhaust { get; set; }

	/// <summary>
	/// Spawned at the exhaust on each backfire.
	/// </summary>
	[Property, Group( "Exhaust" )] public GameObject BackfireEffect { get; set; }

	[Property, Group( "Exhaust" )] public SoundEvent BackfireSound { get; set; }

	/// <summary>
	/// Most backfires a second in the crackle after lifting off.
	/// </summary>
	[Property, Group( "Exhaust" )] public float CrackleRate { get; set; } = 6f;

	/// <summary>
	/// Revs the crackle starts from, as a fraction of max rpm.
	/// </summary>
	[Property, Group( "Exhaust" ), Range( 0, 1 )] public float CrackleRevs { get; set; } = 0.55f;

	/// <summary>
	/// Backfires a second against the rev limiter.
	/// </summary>
	[Property, Group( "Exhaust" )] public float LimiterRate { get; set; } = 3f;

	/// <summary>
	/// Pop on upshifts with the throttle down.
	/// </summary>
	[Property, Group( "Exhaust" )] public bool ShiftPops { get; set; } = true;

	/// <summary>
	/// As the vehicle speeds up it moves through these, each with its own gear sound.
	/// </summary>
	[Property, Group( "Engine" )]
	public List<VehicleSoundGear> Gears { get; set; } =
	[
		new() { MaxSpeed = 0.27f, SpeedApproachFactor = 1.0f },
		new() { MaxSpeed = 0.5f, SpeedApproachFactor = 0.05f },
		new() { MaxSpeed = 0.75f, SpeedApproachFactor = 0.052f },
		new() { MaxSpeed = 0.95f, SpeedApproachFactor = 0.034f },
		new() { MaxSpeed = 1.5f, SpeedApproachFactor = 0.033f },
		new() { MaxSpeed = 2.0f, SpeedApproachFactor = 0.03f },
	];

	[Property, Group( "Engine" )]
	public List<VehicleStateSound> States { get; set; } =
	[
		new() { State = VehicleSoundState.StartIdle, MinTime = 4.0f },
		new() { State = VehicleSoundState.Idle },
		new() { State = VehicleSoundState.Reverse, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear0, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear0Resume, MinTime = 0.75f },
		new() { State = VehicleSoundState.Gear1, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear1Resume, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear2, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear2Resume, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear3, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear3Resume, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear4, MinTime = 0.5f },
		new() { State = VehicleSoundState.Gear4Resume, MinTime = 0.5f },
		new() { State = VehicleSoundState.SlowdownHighSpeed, MinTime = 2.0f },
		new() { State = VehicleSoundState.Slowdown, MinTime = 2.0f },
		new() { State = VehicleSoundState.Turbo, MinTime = 2.5f },
		new() { State = VehicleSoundState.Shutdown },
	];

	[Property, Group( "Impacts" )]
	public List<VehicleCrashSound> CrashSounds { get; set; } =
	[
		new() { MinSpeed = 350, MinSpeedChange = 250, GearLimit = 1 },
		new() { MinSpeed = 450, MinSpeedChange = 350 },
	];

	/// <summary>
	/// Looped while skidding.
	/// </summary>
	[Property, Group( "Tyres" )] public SoundEvent SkidSound { get; set; }

	public VehicleSoundState State => _state;

	private const float SkidThreshold = 10.0f;

	private VehicleSoundState _state = VehicleSoundState.None;
	private float _stateStartTime;
	private int _soundGear;
	private float _speedPercentage;
	private float _lastSpeed;
	private bool _wasOn;

	private SoundHandle _stateSound;
	private SoundEvent _stateSoundEvent;
	private SoundHandle _skidSound;
	private bool _lastSkid;
	private int _lastGear;

	private float _overrunCharge;
	private readonly List<float> _pendingBackfires = new();

	protected override void OnDestroy()
	{
		StopStateSound();
		_skidSound?.Stop();
	}

	protected override void OnUpdate()
	{
		if ( !Vehicle.IsValid() ) return;

		var on = Vehicle.IsEngineOn;
		if ( on && !_wasOn )
			SoundStart();
		_wasOn = on;

		if ( _state != VehicleSoundState.None )
		{
			SoundUpdate( !on );
			UpdateSkid();
		}

		UpdateBackfires();
	}

	private void SoundStart()
	{
		_state = VehicleSoundState.None;
		SoundUpdate( false );
	}

	/// <summary>
	/// Move through the sound gears with the speed and pick the state - the throttle, reverse, boost and speed come
	/// from the host.
	/// </summary>
	private void SoundUpdate( bool exitVehicle )
	{
		var currentSpeed = (Vehicle.SpeedMph / MathF.Max( 1, Vehicle.Engine.MaxSpeed )).Clamp( 0, 1 );

		// Figure out our speed for the purposes of sound playing.
		// We slow the transition down a little to make the gear changes slower.
		if ( Gears.Count > 0 && currentSpeed > _speedPercentage )
		{
			// don't accelerate when the throttle isn't down
			if ( !Vehicle.ThrottleDown )
				currentSpeed = _speedPercentage;

			currentSpeed = VehicleMath.Approach( currentSpeed, _speedPercentage, Time.Delta * Gears[_soundGear.Clamp( 0, Gears.Count - 1 )].SpeedApproachFactor );
		}
		_speedPercentage = currentSpeed.Clamp( 0, 1 );

		RecalculateSoundGear();

		var newState = ChooseState( exitVehicle );
		if ( newState != _state )
		{
			_state = newState;
			OnNewState();
		}
	}

	private void RecalculateSoundGear()
	{
		for ( int i = Gears.Count - 1; i >= 0; i-- )
		{
			var minSpeed = i == 0 ? 0 : Gears[i - 1].MaxSpeed;
			if ( _speedPercentage > minSpeed )
			{
				_soundGear = i;
				break;
			}
		}

		// If we're going in reverse, we want to stay in first gear
		if ( Vehicle.Reversing )
			_soundGear = 0;
	}

	private float MinTime( VehicleSoundState state ) => States.FirstOrDefault( s => s.State == state )?.MinTime ?? 0;
	private SoundEvent StateSound( VehicleSoundState state ) => States.FirstOrDefault( s => s.State == state )?.Sound;

	private VehicleSoundState MapGearToState( int gear ) => gear switch
	{
		0 => Vehicle.Reversing ? VehicleSoundState.Reverse : VehicleSoundState.Gear0,
		1 => VehicleSoundState.Gear1,
		2 => VehicleSoundState.Gear2,
		3 => VehicleSoundState.Gear3,
		_ => VehicleSoundState.Gear4,
	};

	private VehicleSoundState MapGearToMidState( int gear ) => gear switch
	{
		0 => Vehicle.Reversing ? VehicleSoundState.Reverse : VehicleSoundState.Gear0Resume,
		1 => VehicleSoundState.Gear1Resume,
		2 => VehicleSoundState.Gear2Resume,
		3 => VehicleSoundState.Gear3Resume,
		_ => VehicleSoundState.Gear4Resume,
	};

	private VehicleSoundState ChooseState( bool exitVehicle )
	{
		var inStateForMinTime = Time.Now - _stateStartTime > MinTime( _state );
		var throttleDown = Vehicle.ThrottleDown;
		var reverse = Vehicle.Reversing;
		var turbo = Vehicle.Boosting;

		// exit overrides everything else
		if ( exitVehicle )
		{
			return _state is VehicleSoundState.None ? _state : VehicleSoundState.Shutdown;
		}

		// check global state in states that don't mask them
		switch ( _state )
		{
			case VehicleSoundState.Turbo:
				if ( CheckCrash() )
					return VehicleSoundState.Idle;
				break;

			case VehicleSoundState.Reverse:
			case VehicleSoundState.Gear0:
			case VehicleSoundState.Gear1:
			case VehicleSoundState.Gear2:
			case VehicleSoundState.Gear3:
			case VehicleSoundState.Gear4:
			case VehicleSoundState.Slowdown:
			case VehicleSoundState.SlowdownHighSpeed:
			case VehicleSoundState.Gear0Resume:
			case VehicleSoundState.Gear1Resume:
			case VehicleSoundState.Gear2Resume:
			case VehicleSoundState.Gear3Resume:
			case VehicleSoundState.Gear4Resume:
				if ( turbo )
					return VehicleSoundState.Turbo;
				if ( CheckCrash() )
					return VehicleSoundState.Idle;
				break;
		}

		switch ( _state )
		{
			case VehicleSoundState.StartIdle:
				if ( inStateForMinTime || throttleDown )
					return VehicleSoundState.Idle;
				break;

			case VehicleSoundState.Idle:
				if ( inStateForMinTime && throttleDown )
				{
					if ( turbo )
						return VehicleSoundState.Turbo;
					return reverse ? VehicleSoundState.Reverse : VehicleSoundState.Gear0;
				}
				break;

			case VehicleSoundState.Gear0Resume:
			case VehicleSoundState.Gear0:
				if ( (inStateForMinTime && !throttleDown) || reverse )
					return VehicleSoundState.Idle;
				if ( _soundGear > 0 )
					return VehicleSoundState.Gear1;
				break;

			case VehicleSoundState.Gear1Resume:
			case VehicleSoundState.Gear1:
				if ( inStateForMinTime && !throttleDown )
					return VehicleSoundState.Slowdown;
				if ( _soundGear != 1 )
					return MapGearToState( _soundGear );
				break;

			case VehicleSoundState.Gear2Resume:
			case VehicleSoundState.Gear2:
				if ( inStateForMinTime )
				{
					if ( !throttleDown )
						return VehicleSoundState.Slowdown;
					if ( _soundGear != 2 )
						return MapGearToState( _soundGear );
				}
				break;

			case VehicleSoundState.Gear3Resume:
			case VehicleSoundState.Gear3:
				if ( inStateForMinTime )
				{
					if ( !throttleDown )
						return VehicleSoundState.Slowdown;
					if ( _soundGear != 3 )
						return MapGearToState( _soundGear );
				}
				break;

			case VehicleSoundState.Gear4Resume:
			case VehicleSoundState.Gear4:
				if ( inStateForMinTime && !throttleDown )
					return VehicleSoundState.Slowdown;
				if ( _soundGear != 4 )
					return MapGearToMidState( _soundGear );
				break;

			case VehicleSoundState.Reverse:
				if ( inStateForMinTime && !reverse )
					return VehicleSoundState.Slowdown;
				break;

			case VehicleSoundState.SlowdownHighSpeed:
			case VehicleSoundState.Slowdown:
				if ( throttleDown )
					return MapGearToMidState( _soundGear );
				if ( _soundGear == 0 )
					return VehicleSoundState.Idle;
				break;

			case VehicleSoundState.None:
				return VehicleSoundState.StartIdle;

			case VehicleSoundState.Turbo:
				if ( inStateForMinTime && !turbo )
					return MapGearToMidState( _soundGear );
				break;
		}

		return _state;
	}

	private void OnNewState()
	{
		switch ( _state )
		{
			case VehicleSoundState.Shutdown:
				StopStateSound();
				PlayOneShot( StateSound( _state ) );
				break;

			case VehicleSoundState.Idle:
				_lastSpeed = -1;
				PlayLoopingSound( StateSound( _state ) );
				break;

			case VehicleSoundState.Slowdown:
			case VehicleSoundState.SlowdownHighSpeed:
				PlayLoopingSound( StateSound( _soundGear < 2 ? VehicleSoundState.Slowdown : VehicleSoundState.SlowdownHighSpeed ) );
				break;

			case VehicleSoundState.None:
				break;

			default:
				PlayLoopingSound( StateSound( _state ) );
				break;
		}

		_stateStartTime = Time.Now;
	}

	/// <summary>
	/// Play the crash sound for a big enough drop in speed since last time. Returns whether there was one.
	/// </summary>
	private bool CheckCrash()
	{
		var speed = Vehicle.Speed;
		var absSpeed = MathF.Abs( speed );
		var absLastSpeed = MathF.Abs( _lastSpeed );
		var delta = absLastSpeed > absSpeed ? MathF.Abs( _lastSpeed - speed ) : 0;
		_lastSpeed = speed;

		VehicleCrashSound hit = null;

		foreach ( var crash in CrashSounds )
		{
			if ( crash.GearLimit == 0 || _soundGear > crash.GearLimit )
				continue;

			if ( delta > crash.MinSpeedChange && absLastSpeed > crash.MinSpeed )
			{
				hit = crash;
				break;
			}
		}

		if ( hit is null )
		{
			for ( int i = CrashSounds.Count - 1; i >= 0; i-- )
			{
				var crash = CrashSounds[i];
				if ( delta > crash.MinSpeedChange && absLastSpeed > crash.MinSpeed )
				{
					hit = crash;
					break;
				}
			}
		}

		if ( hit is null )
			return false;

		if ( hit.Sound is not null )
			Sound.Play( hit.Sound, WorldPosition );

		return true;
	}

	/// <summary>
	/// The skid sound. We have to slide at least 15% of our speed at higher speeds to make it, otherwise it plays
	/// too often.
	/// </summary>
	private void UpdateSkid()
	{
		const float skidFactor = 0.15f;
		const float minSpeed = SkidThreshold / skidFactor;

		var speed = Vehicle.Speed;
		var threshold = _lastSkid ? SkidThreshold : speed * skidFactor;
		if ( threshold < SkidThreshold )
		{
			// ramp in the skid threshold to avoid the sound at really low speeds unless really skidding
			threshold = VehicleMath.RemapValClamped( MathF.Abs( speed ), 0, minSpeed, SkidThreshold * 8, SkidThreshold );
		}

		var skidding = Vehicle.SkidSpeed > threshold && Vehicle.IsEngineOn;
		if ( skidding != _lastSkid )
		{
			_lastSkid = skidding;

			if ( SkidSound is not null )
			{
				_skidSound?.Stop( 0.1f );
				_skidSound = skidding ? Sound.Play( SkidSound, WorldPosition ) : null;
			}
		}

		if ( _skidSound is not null )
			_skidSound.Position = WorldPosition;
	}

	/// <summary>
	/// Fade out the old state's sound and start the new one.
	/// </summary>
	private void PlayLoopingSound( SoundEvent sound )
	{
		if ( sound == _stateSoundEvent && _stateSound.IsValid() )
			return;

		StopStateSound();

		if ( sound is null ) return;

		_stateSoundEvent = sound;
		_stateSound = Sound.Play( sound, WorldPosition );
		if ( _stateSound.IsValid() )
		{
			_stateSound.Parent = GameObject;
			_stateSound.FollowParent = true;
		}
	}

	private void StopStateSound()
	{
		_stateSound?.Stop( 0.2f );
		_stateSound = null;
		_stateSoundEvent = null;
	}

	private void PlayOneShot( SoundEvent sound )
	{
		if ( sound is null ) return;
		Sound.Play( sound, WorldPosition );
	}

	/// <summary>
	/// The exhaust backfiring. Unburnt fuel lights in the hot pipe: for a moment after lifting off from high revs
	/// (it builds up under hard throttle and dies away over about a second), against the rev limiter, and on hard
	/// upshifts. Each one pops and shoots a flame out of the exhaust.
	/// </summary>
	private void UpdateBackfires()
	{
		var on = Vehicle.IsEngineOn;
		var load = on && Vehicle.ThrottleDown;
		var rev = Vehicle.SoundRpm / MathF.Max( 1, Vehicle.Engine.MaxRpm );
		var dt = Time.Delta;

		// heat and fuel through the pipe: builds over a few tenths of a second of hard throttle, fades once you lift
		_overrunCharge = load
			? _overrunCharge + (1 - _overrunCharge) * MathF.Min( 1, dt / 0.4f )
			: _overrunCharge * MathF.Max( 0, 1 - dt / 0.7f );

		if ( on )
		{
			var crackle = load ? 0 : CrackleRate * _overrunCharge * ((rev - CrackleRevs) / MathF.Max( 0.01f, 0.9f - CrackleRevs )).Clamp( 0, 1 );
			var limiter = load && rev > 0.93f ? LimiterRate : 0;
			if ( Random.Shared.Float() < (crackle + limiter) * dt )
				Backfire();

			// upshift with the throttle down: the ignition cut pops a few times
			var gear = Vehicle.Gear;
			if ( ShiftPops && gear > _lastGear && load )
			{
				var at = Time.Now;
				for ( int i = Random.Shared.Next( 1, 4 ); i > 0; i-- )
				{
					at += Random.Shared.Float( 0.03f, 0.09f );
					_pendingBackfires.Add( at );
				}
			}
			_lastGear = gear;
		}

		for ( int i = _pendingBackfires.Count - 1; i >= 0; i-- )
		{
			if ( _pendingBackfires[i] > Time.Now ) continue;

			_pendingBackfires.RemoveAt( i );
			Backfire();
		}
	}

	private void Backfire()
	{
		var origin = Exhaust.IsValid() ? Exhaust.WorldPosition : WorldPosition;
		if ( BackfireSound is not null )
			Sound.Play( BackfireSound, origin );

		if ( !BackfireEffect.IsValid() || !Exhaust.IsValid() ) return;

		// with a parent the clone's transform is local to it - right on the end of the pipe
		var fx = BackfireEffect.Clone( new CloneConfig { Transform = global::Transform.Zero, Parent = Exhaust, StartEnabled = true } );
		fx.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
	}
}
