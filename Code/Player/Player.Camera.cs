using Sandbox.Movement;

public sealed partial class Player
{
	[Property, Group( "Camera" )] public float SeatedCameraDistance { get; set; } = 200f;
	[Property, Group( "Camera" )] public float SeatedCameraHeight { get; set; } = 40f;
	[Property, Group( "Camera" )] public float SeatedCameraPositionSpeed { get; set; } = 3f;
	[Property, Group( "Camera" )] public float SeatedCameraVelocityScale { get; set; } = 0.1f;

	private ISitTarget _cachedSeat;
	private float _minCameraDistance;
	private float _smoothedDistance;
	private Angles _seatedAngles;
	private Vector3 _lastSeatWorldPos;

	private float roll;

	void PlayerController.IEvents.OnEyeAngles( ref Angles ang )
	{
		var angles = ang;
		IPlayerEvent.Post( x => x.OnCameraMove( ref angles ) );
		ang = angles;
	}

	void PlayerController.IEvents.PostCameraSetup( CameraComponent camera )
	{
		camera.FovAxis = CameraComponent.Axis.Vertical;
		camera.FieldOfView = Screen.CreateVerticalFieldOfView( Preferences.FieldOfView, 9.0f / 16.0f );

		IPlayerEvent.Post( x => x.OnCameraSetup( camera ) );

		ApplyMovementCameraEffects( camera );
		ApplySeatedCameraSetup( camera );

		IPlayerEvent.Post( x => x.OnCameraPostSetup( camera ) );
	}

	private void ApplyMovementCameraEffects( CameraComponent camera )
	{
		if ( Controller.ThirdPerson ) return;
		if ( !GamePreferences.ViewBobbing ) return;

		var r = Controller.WishVelocity.Dot( EyeTransform.Left ) / -250.0f;
		roll = MathX.Lerp( roll, r, Time.Delta * 10.0f, true );

		camera.WorldRotation *= new Angles( 0, 0, roll );
	}

	private void ApplySeatedCameraSetup( CameraComponent camera )
	{
		if ( !Controller.ThirdPerson )
		{
			_cachedSeat = null;
			return;
		}

		var seat = GetComponentInParent<ISitTarget>( false );
		if ( seat is null )
		{
			_cachedSeat = null;
			return;
		}

		var seatGo = (seat as Component).GameObject;
		var seatPos = seatGo.WorldPosition + Vector3.Up * SeatedCameraHeight;

		if ( seat != _cachedSeat )
		{
			_cachedSeat = seat;
			_minCameraDistance = MathF.Max( SeatedCameraDistance, RebuildContraptionBounds( seatGo ) );
			_seatedAngles = camera.WorldRotation.Angles();
			_lastSeatWorldPos = seatPos;
			_smoothedDistance = _minCameraDistance;
		}

		_seatedAngles.yaw += Input.AnalogLook.yaw;
		_seatedAngles.pitch = (_seatedAngles.pitch + Input.AnalogLook.pitch).Clamp( -89, 89 );

		// Derive velocity from position delta and add it to the target distance
		var speed = (seatPos - _lastSeatWorldPos).Length / Time.Delta;
		_lastSeatWorldPos = seatPos;
		var targetDistance = _minCameraDistance + speed * SeatedCameraVelocityScale;

		// Smooth orbit distance
		_smoothedDistance = _smoothedDistance.LerpTo( targetDistance, Time.Delta * SeatedCameraPositionSpeed );

		// Compose rotation: yaw around world up, then pitch around local right, no gimbal lock
		var camRot = Rotation.FromYaw( _seatedAngles.yaw ) * Rotation.FromPitch( _seatedAngles.pitch );
		var desiredPos = seatPos + camRot.Backward * _smoothedDistance;

		var tr = Scene.Trace.FromTo( seatPos, desiredPos ).Radius( 8f ).WithTag( "world" ).IgnoreGameObjectHierarchy( GameObject.Root ).Run();
		var camPos = tr.Hit ? tr.HitPosition + (seatPos - desiredPos).Normal * 4f : desiredPos;

		camera.WorldPosition = camPos;
		camera.WorldRotation = Rotation.LookAt( seatPos - camPos, Vector3.Up );
	}

	private float RebuildContraptionBounds( GameObject seatGo )
	{
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( seatGo );

		var totalBounds = new BBox();
		var initialized = false;
		foreach ( var obj in builder.Objects )
		{
			if ( obj.Tags.Has( "player" ) ) continue;
			var b = obj.GetBounds();
			totalBounds = initialized ? totalBounds.AddBBox( b ) : b;
			initialized = true;
		}

		return totalBounds.Size.Length;
	}
}
