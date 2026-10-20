/// <summary>
/// Posing the model, a <see cref="SkinnedModelRenderer"/>. Every machine poses its own from wherever it has the body:
/// it traces for the wheels itself and rolls them at the body's speed, so the host's wheel state never goes over the
/// network.
///
/// The bones, where {c} is fl, fr, rl or rr:
/// <list type="bullet">
/// <item><c>wheel_{c}</c> - the knuckle, where the arm meets it (at the front, on the steering axis). It moves up and
/// down with the suspension. The arm and shock are skinned from the body to this, so they follow.</item>
/// <item><c>wheel_{c}_steer</c> - a child of the knuckle where it is, that turns to steer: the front axle. Without
/// one, a front knuckle steers itself.</item>
/// <item><c>wheel_{c}_spin</c> - a child of those at the tyre's centre, which is where the suspension holds the
/// wheel. It rolls about whichever of its axes runs closest to the axle.</item>
/// <item><c>steering_wheel</c> - turns about <see cref="SteeringWheelAxis"/>.</item>
/// </list>
/// Any other bone rides on its parent.
/// </summary>
public sealed partial class FourWheelVehicle
{
	private static readonly string[] Corners = ["fl", "fr", "rl", "rr"];

	private static string WheelBone( string corner ) => $"wheel_{corner}";
	private static string SteerBone( string corner ) => $"wheel_{corner}_steer";
	private static string SpinBone( string corner ) => $"wheel_{corner}_spin";
	private const string SteeringWheelBone = "steering_wheel";

	private SkinnedModelRenderer _renderer;
	private Model _rigModel;
	private Rig _rig;

	/// <summary>
	/// The rig the wheels were set up from.
	/// </summary>
	private Rig _wheelsRig;

	private readonly float[] _spin = new float[WheelCount];

	/// <summary>
	/// The model's rig, or null if there's no model or it doesn't have the wheel bones.
	/// </summary>
	private Rig GetRig()
	{
		if ( !_renderer.IsValid() )
			_renderer = GetComponent<SkinnedModelRenderer>();

		var model = _renderer?.Model;
		if ( model != _rigModel )
		{
			_rigModel = model;
			_rig = model is null || model.IsError ? null : Rig.Create( model );
		}

		return _rig;
	}

	/// <summary>
	/// Put the wheels where the ground is, steer and roll them, and turn the steering wheel.
	/// </summary>
	private void PoseRig()
	{
		var rig = GetRig();
		if ( rig is null ) return;

		if ( !_renderer.CreateBoneObjects )
			_renderer.CreateBoneObjects = true;

		rig.Reset();

		var tx = WorldTransform;
		for ( int i = 0; i < WheelCount; i++ )
		{
			var axle = AxleOf( i );
			var hardPoint = rig.HardPoint( i );
			var steer = i < 2 ? Rotation.FromAxis( Vector3.Up, -SteerAngle ) : Rotation.Identity;

			// how far down the ground is
			var contact = WheelSweep( tx.PointToWorld( hardPoint ), tx.Rotation.Down, tx.Rotation * (steer * Vector3.Left), axle.WheelRadius, axle.TyreWidth, axle.SpringAdditionalLength );
			var travel = (contact.Hit ? contact.Distance : axle.SpringAdditionalLength + axle.WheelRadius) - axle.WheelRadius;
			if ( rig.Steer[i] >= 0 )
			{
				rig.Move( rig.Wheel[i], Vector3.Down * travel, Rotation.Identity );
				rig.Move( rig.Steer[i], Vector3.Zero, steer );
			}
			else
			{
				rig.Move( rig.Wheel[i], Vector3.Down * travel, steer );
			}

			// roll, unless the wheels are locked
			if ( !WheelsLocked )
				_spin[i] = (_spin[i] + (Speed / axle.WheelRadius).RadianToDegree() * Time.Delta) % 360f;

			rig.Move( rig.Spin[i], Vector3.Zero, Rotation.FromAxis( rig.Axle( i ), _spin[i] ) );
		}

		if ( rig.SteeringWheel >= 0 )
			rig.Move( rig.SteeringWheel, Vector3.Zero, Rotation.FromAxis( SteeringWheelAxis, -_steerPose * SteeringWheelAngle ) );

		// parents first: moving a bone object carries its children
		foreach ( var bone in rig.Order )
		{
			var pose = rig.Pose( bone );
			var boneObject = _renderer.GetBoneObject( bone );
			if ( !boneObject.IsValid() ) continue;

			boneObject.Flags |= GameObjectFlags.ProceduralBone;
			boneObject.WorldTransform = tx.ToWorld( pose );
		}
	}

	/// <summary>
	/// A model's bones, where they rest in body space, and their pose this frame.
	/// </summary>
	private sealed class Rig
	{
		public Model Model;
		public global::Transform[] Rest;
		public int[] Parent;

		/// <summary>
		/// Every bone, parents before their children.
		/// </summary>
		public int[] Order;

		public readonly int[] Wheel = new int[WheelCount];
		public readonly int[] Steer = new int[WheelCount];
		public readonly int[] Spin = new int[WheelCount];
		public int SteeringWheel;

		/// <summary>
		/// How each bone has been moved this frame, in its own rest frame.
		/// </summary>
		private global::Transform[] _moved;
		private global::Transform[] _pose;

		public static Rig Create( Model model )
		{
			var bones = model.Bones.AllBones.OrderBy( b => b.Index ).ToArray();
			int Find( string name ) => model.Bones.HasBone( name ) ? model.Bones.GetBone( name ).Index : -1;

			var rig = new Rig
			{
				Model = model,
				Rest = [.. bones.Select( b => b.LocalTransform )],
				Parent = [.. bones.Select( b => b.Parent?.Index ?? -1 )],
				SteeringWheel = Find( SteeringWheelBone ),
				_moved = new global::Transform[bones.Length],
				_pose = new global::Transform[bones.Length],
			};

			for ( int i = 0; i < WheelCount; i++ )
			{
				rig.Wheel[i] = Find( WheelBone( Corners[i] ) );
				rig.Steer[i] = Find( SteerBone( Corners[i] ) );
				rig.Spin[i] = Find( SpinBone( Corners[i] ) );
				if ( rig.Wheel[i] < 0 || rig.Spin[i] < 0 )
					return null;
			}

			int Depth( int b ) => rig.Parent[b] < 0 ? 0 : 1 + Depth( rig.Parent[b] );
			rig.Order = [.. Enumerable.Range( 0, bones.Length ).OrderBy( Depth )];

			return rig;
		}

		/// <summary>
		/// Where the suspension holds the wheel: the tyre's centre at rest.
		/// </summary>
		public Vector3 HardPoint( int wheel ) => Rest[Spin[wheel]].Position;

		/// <summary>
		/// The axle a wheel rolls about at rest, pointing left: the spin bone's axis closest to the car's left, so it
		/// doesn't matter which way the bone was built.
		/// </summary>
		public Vector3 Axle( int wheel )
		{
			var rotation = Rest[Spin[wheel]].Rotation;
			var axle = new[] { rotation.Forward, rotation.Left, rotation.Up }.MaxBy( a => MathF.Abs( a.y ) );
			return axle.y < 0 ? -axle : axle;
		}

		public void Reset() => Array.Fill( _moved, global::Transform.Zero );

		/// <summary>
		/// Move a bone by an offset and turn it about itself, both in body space as it sits at rest. Its children go
		/// with it.
		/// </summary>
		public void Move( int bone, Vector3 offset, Rotation rotation )
		{
			var rest = Rest[bone].Rotation;
			_moved[bone] = new global::Transform( rest.Inverse * offset, rest.Inverse * rotation * rest );
		}

		/// <summary>
		/// A bone's pose in body space this frame: riding on its parent as it did at rest, then moved. Call it in
		/// <see cref="Order"/>, so the parent's is ready.
		/// </summary>
		public global::Transform Pose( int bone )
		{
			var parent = Parent[bone];
			var follow = parent < 0 ? Rest[bone] : _pose[parent].ToWorld( Rest[parent].ToLocal( Rest[bone] ) );
			return _pose[bone] = follow.ToWorld( _moved[bone] );
		}
	}
}
