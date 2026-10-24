/// <summary>
/// The driver's hands on the steering wheel, by IK. Put the hand targets under the <c>steering_wheel</c> bone's object
/// so they turn with it (<see cref="PoseRig"/> poses it before we read them), each oriented the way the hand should
/// grip. While the horn's held (<see cref="VehicleSounds.HornDown"/>) the right hand moves off the wheel onto
/// <see cref="HornHand"/> and opens flat.
///
/// The seated pose sits too far back to reach a turned wheel, and the body's aim only bends the neck, so the whole body
/// model tilts forward about the hips (<see cref="DriverTilt"/>) before it's animated - the IK then solves from there.
/// Foot targets (under the pedal bones, say) keep the legs from swinging down through the floor with it.
///
/// Runs on everyone, from the synced steering and horn, on whoever's in <see cref="Seat"/>.
/// </summary>
public sealed partial class FourWheelVehicle
{
	[Property, Group( "Driver Hands" )] public GameObject LeftHand { get; set; }
	[Property, Group( "Driver Hands" )] public GameObject RightHand { get; set; }

	/// <summary>
	/// Where the right hand goes to press the horn.
	/// </summary>
	[Property, Group( "Driver Hands" )] public GameObject HornHand { get; set; }

	/// <summary>
	/// How long the right hand takes to move between the wheel and the horn, seconds.
	/// </summary>
	[Property, Group( "Driver Hands" )] public float HornHandTime { get; set; } = 0.1f;

	/// <summary>
	/// The animgraph hold type that shapes the right hand on the horn (<c>holdtype</c>), and its hand pose
	/// (<c>holdtype_pose_hand</c>) - tune these for the open palm. Empty leaves the hand as it is.
	/// </summary>
	[Property, Group( "Driver Hands" )] public string HornHoldType { get; set; } = "holditem";
	[Property, Group( "Driver Hands" )] public float HornHandPose { get; set; } = 1f;

	/// <summary>
	/// Optional foot targets. With the body tilted, the legs swing down too - these hold the feet where they belong.
	/// </summary>
	[Property, Group( "Driver Hands" )] public GameObject LeftFoot { get; set; }
	[Property, Group( "Driver Hands" )] public GameObject RightFoot { get; set; }

	/// <summary>
	/// How far the driver's whole body tilts forward towards the wheel, degrees, about <see cref="TiltPivot"/>.
	/// </summary>
	[Property, Group( "Driver Hands" ), Range( 0, 45 )] public float DriverTilt { get; set; } = 15f;

	/// <summary>
	/// Most the driver sways to the side in a turn, degrees, thrown to the outside of it.
	/// </summary>
	[Property, Group( "Driver Sway" ), Range( 0, 30 )] public float MaxSway { get; set; } = 10f;

	/// <summary>
	/// Cornering (sideways acceleration, in/s²) that sways the driver all the way. The tuning's gravity is 600.
	/// </summary>
	[Property, Group( "Driver Sway" )] public float SwayFullAcceleration { get; set; } = 600f;

	/// <summary>
	/// The sway's a spring: how stiffly it follows the cornering, and how quickly it stops wobbling.
	/// </summary>
	[Property, Group( "Driver Sway" )] public float SwayStiffness { get; set; } = 60f;
	[Property, Group( "Driver Sway" )] public float SwayDamping { get; set; } = 7f;

	/// <summary>
	/// What the body tilts about. Empty, it's the driver's pelvis, so they stay sat on the seat.
	/// </summary>
	[Property, Group( "Driver Hands" )] public GameObject TiltPivot { get; set; }

	private const string LeftHandIk = "hand_left";
	private const string RightHandIk = "hand_right";
	private const string LeftFootIk = "foot_left";
	private const string RightFootIk = "foot_right";

	private Player _driver;
	private SkinnedModelRenderer _handsRenderer;
	private VehicleSounds _sounds;
	private float _hornHand;
	private bool _palmOpen;

	private float _sway;
	private float _swayVelocity;
	private float _lastYaw;
	private bool _hasLastYaw;

	protected override void OnDisabled() => ReleaseHands();
	protected override void OnDestroy() => ReleaseHands();

	/// <summary>
	/// After <see cref="PoseRig"/>, so the targets on the steering wheel are where it is this frame.
	/// </summary>
	private void UpdateDriverHands()
	{
		var driver = Seat?.GetOccupant()?.GetComponent<Player>();
		var renderer = driver?.Controller?.Renderer;
		if ( !renderer.IsValid() || (!LeftHand.IsValid() && !RightHand.IsValid()) )
		{
			ReleaseHands();
			return;
		}

		// someone else got in
		if ( renderer != _handsRenderer )
		{
			ReleaseHands();
			_driver = driver;
			_handsRenderer = renderer;
		}

		if ( !_sounds.IsValid() )
			_sounds = GetComponent<VehicleSounds>();

		var horn = HornHand.IsValid() && (_sounds?.HornDown ?? false);
		_hornHand = _hornHand.Approach( horn ? 1 : 0, Time.Delta / MathF.Max( HornHandTime, 0.001f ) );

		// the palm opens as the hand gets most of the way to the horn, and closes again as it leaves
		_palmOpen = _hornHand > 0.5f;

		UpdateSway();
	}

	/// <summary>
	/// The hand and foot IK targets. <see cref="SkinnedModelRenderer.SetIk"/> takes them relative to the body as it is
	/// when it's called, so this has to come after the tilt or the targets get carried forward with it.
	/// </summary>
	private void SetDriverIk( SkinnedModelRenderer renderer )
	{
		SetIk( renderer, LeftHandIk, LeftHand );
		SetIk( renderer, LeftFootIk, LeftFoot );
		SetIk( renderer, RightFootIk, RightFoot );

		var right = RightHand.IsValid() ? RightHand.WorldTransform : (global::Transform?)null;
		if ( HornHand.IsValid() && _hornHand > 0 )
		{
			var to = HornHand.WorldTransform;
			var from = right ?? to;
			var t = _hornHand * _hornHand * (3 - 2 * _hornHand); // ease in and out
			right = new global::Transform( Vector3.Lerp( from.Position, to.Position, t ), Rotation.Slerp( from.Rotation, to.Rotation, t ) );
		}

		if ( right is { } rightTx )
			renderer.SetIk( RightHandIk, rightTx );
		else
			renderer.ClearIk( RightHandIk );
	}

	private void UpdateSway()
	{
		var dt = MathF.Min( Time.Delta, 0.05f );
		var yaw = WorldRotation.Angles().yaw;

		var yawRate = 0f;
		if ( _hasLastYaw && dt > 0 )
			yawRate = MathX.DeltaDegrees( _lastYaw, yaw ).DegreeToRadian() / dt;

		_lastYaw = yaw;
		_hasLastYaw = true;

		var lateral = Speed * yawRate;
		var target = MaxSway * (lateral / MathF.Max( SwayFullAcceleration, 1f )).Clamp( -1f, 1f );

		_swayVelocity += ((target - _sway) * SwayStiffness - _swayVelocity * SwayDamping) * dt;
		_sway = (_sway + _swayVelocity * dt).Clamp( -MaxSway * 1.5f, MaxSway * 1.5f );
	}

	private static void SetIk( SkinnedModelRenderer renderer, string ik, GameObject target )
	{
		if ( target.IsValid() )
			renderer.SetIk( ik, target.WorldTransform );
		else
			renderer.ClearIk( ik );
	}

	internal void ApplyDriverPose()
	{
		if ( !_handsRenderer.IsValid() || !_driver.IsValid() ) return;

		var body = _handsRenderer.GameObject;

		// the renderer has to be on its own object under the player, or we'd be moving the player
		if ( body != _driver.GameObject )
		{
			var root = _driver.WorldTransform;
			var pivot = TiltPivot.IsValid() ? TiltPivot.WorldPosition : PelvisPivot( body, root );
			// lean forward over the wheel, and sway to the side (rolling about the car's length: positive leans right)
			var tilt = Rotation.FromAxis( WorldRotation.Forward, _sway ) * Rotation.FromAxis( WorldRotation.Left, DriverTilt );

			body.WorldTransform = new global::Transform( pivot + tilt * (root.Position - pivot), tilt * root.Rotation, body.WorldScale );
		}

		SetDriverIk( _handsRenderer );
		ApplyHornHandPose( _handsRenderer );
	}

	/// <summary>
	/// Where the driver's pelvis is with the body upright - so it stays on the seat while the body tilts over it.
	/// </summary>
	private Vector3 PelvisPivot( GameObject body, global::Transform root )
	{
		var model = _handsRenderer.Model;
		if ( model is null || !model.Bones.HasBone( PelvisBone ) ) return root.Position;

		if ( !_handsRenderer.CreateBoneObjects )
			_handsRenderer.CreateBoneObjects = true;

		var pelvis = _handsRenderer.GetBoneObject( model.Bones.GetBone( PelvisBone ).Index );
		if ( !pelvis.IsValid() ) return root.Position;

		var inBody = body.WorldTransform.PointToLocal( pelvis.WorldPosition );
		return root.PointToWorld( inBody );
	}

	private const string PelvisBone = "pelvis";

	/// <summary>
	/// Shape the right hand for the horn with the animgraph's hold type, right hand only - the arm's on IK, so only the
	/// fingers take it. <see cref="Player"/> puts the hold type back to none every update while there's no weapon out,
	/// so this is set from the late pose step each frame it's wanted and simply stops when it isn't.
	/// </summary>
	private void ApplyHornHandPose( SkinnedModelRenderer renderer )
	{
		if ( !_palmOpen || string.IsNullOrEmpty( HornHoldType ) ) return;

		renderer.Set( "holdtype", HornHoldType );
		renderer.Set( "holdtype_handedness", 1 );
		renderer.Set( "holdtype_pose_hand", HornHandPose );
	}

	/// <summary>
	/// Give the last driver their hands, feet, fingers and posture back.
	/// </summary>
	private void ReleaseHands()
	{
		if ( _handsRenderer.IsValid() )
		{
			_handsRenderer.ClearIk( LeftHandIk );
			_handsRenderer.ClearIk( RightHandIk );
			_handsRenderer.ClearIk( LeftFootIk );
			_handsRenderer.ClearIk( RightFootIk );

			// back upright on the player (the body sits at its origin in the prefab)
			if ( _driver.IsValid() && _handsRenderer.GameObject != _driver.GameObject )
			{
				_handsRenderer.GameObject.LocalPosition = Vector3.Zero;
				_handsRenderer.GameObject.LocalRotation = Rotation.Identity;
			}
		}

		_driver = null;
		_handsRenderer = null;
		_hornHand = 0;
		_palmOpen = false;
		_sway = 0;
		_swayVelocity = 0;
		_hasLastYaw = false;
	}
}

/// <summary>
/// Tilts drivers forward over their wheels and sets their IK (<see cref="FourWheelVehicle.ApplyDriverPose"/>) once every component has
/// updated - the player controller places the body in its update, so ours has to land after it - and before the bones
/// are animated, so the IK solves from the tilted body.
/// </summary>
public sealed class VehicleDriverPoseSystem : GameObjectSystem<VehicleDriverPoseSystem>
{
	public VehicleDriverPoseSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, -1000, PoseDrivers, "PoseVehicleDrivers" );
	}

	private void PoseDrivers()
	{
		foreach ( var vehicle in Scene.GetAll<FourWheelVehicle>() )
			vehicle.ApplyDriverPose();
	}
}
