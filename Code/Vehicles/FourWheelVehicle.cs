/// <summary>
/// A drivable four wheel vehicle. It goes on the vehicle body, a normal rigidbody that still collides, tumbles and
/// can be picked up.
/// <list type="bullet">
/// <item>This file - the seat, the engine starting and stopping, what's synced to everyone</item>
/// <item>FourWheelVehicle.Driver.cs - the driver's keys into throttle, brake, steering, boost and handbrake</item>
/// <item>FourWheelVehicle.Drivetrain.cs - engine, gearbox, brakes, steering, power slides and boost</item>
/// <item>FourWheelVehicle.Wheels.cs - the wheels: suspension, anti-roll bars, drive and grip, and their visuals</item>
/// </list>
/// The wheels aren't bodies: each finds the ground below it and pushes the body where they meet. The wheel objects
/// are only the visuals.
/// </summary>
[Title( "Four Wheel Vehicle" ), Category( "Vehicles" ), Icon( "directions_car" )]
public sealed partial class FourWheelVehicle : Component, IPlayerControllable
{
	public const int WheelCount = 4;

	/// <summary>
	/// The driver's seat. Whoever sits here drives.
	/// </summary>
	[Property] public BaseChair Seat { get; set; }

	/// <summary>
	/// Wheel models: front left, front right, rear left, rear right. Where they sit in the prefab is where they rest
	/// on the suspension. They're only visuals - they shouldn't have colliders or rigidbodies of their own.
	/// </summary>
	[Property, Group( "Wheels" )] public List<GameObject> Wheels { get; set; } = [];

	/// <summary>
	/// Optional visuals that follow the front wheels' steering and suspension (hubs, uprights).
	/// They should be children of this object, and not parents of the wheels.
	/// </summary>
	[Property, Group( "Wheels" )] public GameObject FrontLeftHub { get; set; }

	/// <inheritdoc cref="FrontLeftHub"/>
	[Property, Group( "Wheels" )] public GameObject FrontRightHub { get; set; }

	[Property, Group( "Wheels" )] public GameObject SteeringWheel { get; set; }

	/// <summary>
	/// Local axis of the steering column, pointing at the driver.
	/// </summary>
	[Property, Group( "Wheels" )] public Vector3 SteeringWheelAxis { get; set; } = Vector3.Backward;

	/// <summary>
	/// How far the steering wheel turns at full lock, in degrees.
	/// </summary>
	[Property, Group( "Wheels" )] public float SteeringWheelAngle { get; set; } = 90f;

	/// <summary>
	/// How far inboard of the front wheels' centres their steering axis (kingpin) is, in inches. The wheel and its hub
	/// swing around it as they steer. Zero turns them on the spot.
	/// </summary>
	[Property, Group( "Wheels" )] public float KingpinInset { get; set; }

	[Property, Group( "Tuning" ), InlineEditor] public VehicleBodyParams Body { get; set; } = new();
	[Property, Group( "Tuning" ), InlineEditor] public VehicleEngineParams Engine { get; set; } = new();
	[Property, Group( "Tuning" ), InlineEditor] public VehicleSteeringParams Steering { get; set; } = new();

	[Property, Group( "Tuning" ), InlineEditor]
	public VehicleAxleParams FrontAxle { get; set; } = new() { WheelRadius = 18f, TorqueFactor = 0.3f, BrakeFactor = 0.4f };

	[Property, Group( "Tuning" ), InlineEditor]
	public VehicleAxleParams RearAxle { get; set; } = new() { WheelRadius = 22f, TorqueFactor = 0.7f, BrakeFactor = 0.6f };

	/// <summary>
	/// The gravity the tuning is for, in inches per second squared. The vehicle falls at this rather than the scene's
	/// gravity. Zero uses the scene's gravity.
	/// </summary>
	[Property, Group( "Tuning" )] public float Gravity { get; set; } = 600f;

	[Property, Group( "Input" )] public ClientInput Forward { get; set; } = new() { Action = "Forward" };
	[Property, Group( "Input" )] public ClientInput Back { get; set; } = new() { Action = "Backward" };
	[Property, Group( "Input" )] public ClientInput Left { get; set; } = new() { Action = "Left" };
	[Property, Group( "Input" )] public ClientInput Right { get; set; } = new() { Action = "Right" };
	[Property, Group( "Input" )] public ClientInput Boost { get; set; } = new() { Action = "Run" };
	[Property, Group( "Input" )] public ClientInput Handbrake { get; set; } = new() { Action = "Jump" };

	[Property, Group( "Debug" )] public bool DrawDebug { get; set; }

	/// <summary>
	/// Where the wheels, hubs and steering wheel sit relative to the body, taken from the prefab the first time the
	/// vehicle starts. It's saved with the vehicle, so a copy (duplicator, save game, network snapshot) can put them
	/// back - by then they've been moving with the suspension and steering.
	/// </summary>
	[Property, Hide] public VehicleRestPose RestPose { get; set; }

	/// <summary>
	/// Front wheel steering angle in degrees, positive to the right.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public float SteerAngle { get; set; }

	/// <summary>
	/// The handbrake has the wheels locked.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool WheelsLocked { get; set; }

	[Sync( SyncFlags.FromHost )] public bool IsEngineOn { get; private set; }

	/// <summary>
	/// Engine speed for sound: the gearbox's, or the free revving engine's while revving in neutral.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public float SoundRpm { get; private set; }

	[Sync( SyncFlags.FromHost )] public int Gear { get; private set; }

	/// <summary>
	/// The driver is accelerating (not braking, not on the handbrake).
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool ThrottleDown { get; private set; }

	[Sync( SyncFlags.FromHost )] public bool Reversing { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool Boosting { get; private set; }

	/// <summary>
	/// Fastest a wheel is sliding over the ground, in/s.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public float SkidSpeed { get; private set; }

	/// <summary>
	/// Forward speed, in/s, negative backwards.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public float Speed { get; private set; }

	/// <summary>
	/// Speed in miles per hour, either way.
	/// </summary>
	public int SpeedMph => (int)MathF.Abs( InsToMph( Speed ) );

	private Rigidbody _body;
	private bool _carSystemReady;
	private bool _hasDriver;
	private bool _warnedInvalid;

	private Transform[] _wheelRest;
	private float _steerPose;

	/// <summary>
	/// How fast the engine revs up and runs down in neutral, rpm a second.
	/// </summary>
	private const float FreeRevRiseRate = 5000f;
	private const float FreeRevFallRate = 2500f;

	private float _freeRevRpm;

	protected override void OnAwake()
	{
		// Where everything sits in the prefab, relative to the body
		if ( RestPose is null || RestPose.Wheels.Count != Wheels.Count )
		{
			RestPose = new VehicleRestPose
			{
				Wheels = Wheels.Select( w => w.IsValid() ? WorldTransform.ToLocal( w.WorldTransform ) : global::Transform.Zero ).ToList(),
				FrontLeftHub = FrontLeftHub.IsValid() ? FrontLeftHub.LocalTransform : default,
				FrontRightHub = FrontRightHub.IsValid() ? FrontRightHub.LocalTransform : default,
				SteeringWheel = SteeringWheel.IsValid() ? SteeringWheel.LocalRotation : Rotation.Identity,
			};
		}

		_wheelRest = RestPose.Wheels.ToArray();

		// back on their rest pose - they could be anywhere if we were copied while driving
		for ( int i = 0; i < Wheels.Count; i++ )
		{
			if ( Wheels[i].IsValid() )
				Wheels[i].WorldTransform = WorldTransform.ToWorld( _wheelRest[i] );
		}
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			InitCarSystem();
	}

	/// <summary>
	/// Set up the body and the wheels from the tuning.
	/// </summary>
	private void InitCarSystem()
	{
		_carSystemReady = false;

		_body = GetComponent<Rigidbody>();
		if ( !_body.IsValid() || Wheels.Count < WheelCount || Wheels.Take( WheelCount ).Any( w => !w.IsValid() ) )
		{
			if ( !_warnedInvalid )
				Log.Warning( $"{GameObject.Name}: a four wheel vehicle needs a Rigidbody and {WheelCount} wheels" );

			_warnedInvalid = true;
			return;
		}

		// the tuning's mass and centre of mass (zero mass is the model's own)
		_body.MassOverride = Math.Max( 0, Body.MassOverride );
		_body.ResetInertiaTensor();

		if ( Body.MassCenterOverride != Vector3.Zero )
		{
			_body.OverrideMassCenter = true;
			_body.MassCenterOverride = Body.MassCenterOverride;
		}

		_bodyMass = _body.PhysicsBody.Mass;
		_body.GravityScale = GravityScale;

		for ( int i = 0; i < WheelCount; i++ )
		{
			_wheels[i] = CreateWheel( Wheels[i], AxleOf( i ), _wheelRest[i] );
		}

		InitDrivetrain();
		AddWheelMass();
		_carSystemReady = true;

		// make sure the first steering update gets through
		_steeringAngle = -1f;
		SetSteeringAngle( 0f );
	}

	/// <summary>
	/// The wheel's centre, spin axis and width in its own space, from the model's collision hull: the middle of the
	/// hull, and the direction it's thinnest in - a wheel is taller than it is wide - cambered or not. The axle is
	/// turned to point left of the body at rest, so a positive spin about it rolls forward.
	/// </summary>
	private static (Vector3 Center, Vector3 Axle, float Width) WheelShape( GameObject wheel, Transform rest )
	{
		var model = wheel.GetComponent<ModelRenderer>( true )?.Model ?? wheel.GetComponent<ModelCollider>( true )?.Model;
		var hull = model?.Physics?.Parts?.FirstOrDefault()?.Hulls?.FirstOrDefault();
		if ( hull is null )
			return (model?.Bounds.Center ?? Vector3.Zero, Vector3.Left, MathF.Max( 1, model?.Bounds.Size.y ?? 10 ));

		var center = hull.Bounds.Center;
		var points = hull.GetLines().SelectMany( l => new[] { l.Start - center, l.End - center } ).ToArray();

		// covariance of the hull's points
		float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
		foreach ( var d in points )
		{
			xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
			yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
		}

		// the least spread direction is the biggest eigenvector of (trace - covariance), power iterate for it
		var trace = xx + yy + zz;
		var axle = Vector3.Left;
		for ( int i = 0; i < 64; i++ )
		{
			axle = new Vector3(
				(trace - xx) * axle.x - xy * axle.y - xz * axle.z,
				-xy * axle.x + (trace - yy) * axle.y - yz * axle.z,
				-xz * axle.x - yz * axle.y + (trace - zz) * axle.z ).Normal;
		}

		if ( (rest.Rotation * axle).Dot( Vector3.Left ) < 0 )
			axle = -axle;

		var width = points.Max( p => p.Dot( axle ) ) - points.Min( p => p.Dot( axle ) );
		return (center, axle, MathF.Max( 1, width ));
	}

	/// <summary>
	/// Driven from the seat by <see cref="ControlSystem"/>, before our fixed update, with the driver's input active.
	/// </summary>
	void IPlayerControllable.OnControl()
	{
		// no driving with the engine off
		if ( !_carSystemReady || !IsEngineOn ) return;

		// upside down, the wheels can't drive (including boost)
		var overturned = IsOverturned();

		UpdateDriverControls( new DriverInput
		{
			Forward = Forward.Down() && !overturned,
			Back = Back.Down() && !overturned,
			Left = Left.Down(),
			Right = Right.Down(),
			Boost = Boost.Down() && !overturned,
			Handbrake = Handbrake.Down(),
		}, Time.Delta );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		// set up late (hotload), or rebuild if a wheel went missing
		if ( !_carSystemReady || !_body.IsValid() || _wheels.Any( w => w is null || !w.GameObject.IsValid() ) )
		{
			InitCarSystem();
			if ( !_carSystemReady ) return;
		}

		UpdateDriver();
		UpdateDrivetrain( Time.Delta, _controls );
		Simulate( Time.Delta );
		PublishState();
	}

	/// <summary>
	/// Sync what everyone else needs: the steering and wheel visuals, and the engine state for sounds.
	/// </summary>
	private void PublishState()
	{
		SteerAngle = _state.SteeringAngle;
		WheelsLocked = _handbrakeOn;

		// Revving in neutral on the handbrake: the engine spins up quickly towards the limiter and runs down slower
		var revving = IsEngineOn && _freeRevving;
		_freeRevRpm = _freeRevRpm.Approach( revving ? Engine.MaxRpm * 0.98f : 0, (revving ? FreeRevRiseRate : FreeRevFallRate) * Time.Delta );

		SoundRpm = MathF.Max( _state.EngineRpm, _freeRevRpm );
		Gear = _state.Gear;
		ThrottleDown = IsEngineOn && (_throttleDown || revving);
		Reversing = _controls.Throttle < 0;
		Boosting = IsBoosting();
		SkidSpeed = _state.SkidSpeed;
		Speed = _state.Speed;
	}

	/// <summary>
	/// The engine starts and stops as people get in and out of the seat. Getting out puts the handbrake on.
	/// </summary>
	private void UpdateDriver()
	{
		var occupied = Seat.IsValid() && Seat.IsOccupied;
		if ( occupied == _hasDriver ) return;

		_hasDriver = occupied;
		IsEngineOn = occupied;

		if ( !occupied )
		{
			ResetControls();
			OnDriverExit();
		}
	}

	public bool IsOverturned() => WorldRotation.Up.Dot( Vector3.Up ) < 0f;

	protected override void OnUpdate()
	{
		// the steering wheel follows the front wheels, a little behind
		_steerPose = _steerPose.LerpTo( SteerAngle / Steering.DegreesSlow, 1 - MathF.Pow( 0.8f, Time.Delta * 50 ) );
		if ( SteeringWheel.IsValid() && RestPose is not null )
		{
			SteeringWheel.LocalRotation = Rotation.FromAxis( SteeringWheelAxis, -_steerPose * SteeringWheelAngle ) * RestPose.SteeringWheel;
		}

		UpdateWheelVisuals();

		if ( Networking.IsHost && _carSystemReady )
		{
			UpdateDebugHud( ShouldDrawDebug() );
		}
	}
}
