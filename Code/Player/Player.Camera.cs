using Sandbox.Movement;

public sealed partial class Player
{
	[Property, Group( "Camera" )] public float SeatedCameraDistance { get; set; } = 200f;
	[Property, Group( "Camera" )] public float SeatedCameraHeight { get; set; } = 40f;
	[Property, Group( "Camera" )] public float SeatedCameraPositionSpeed { get; set; } = 3f;
	[Property, Group( "Camera" )] public float SeatedCameraRollSpeed { get; set; } = 2f;
	[Property, Group( "Camera" )] public float SeatedCameraVelocityScale { get; set; } = 0.1f;

	private ISitTarget _cachedSeat;
	private float _minCameraDistance;
	private float _smoothedDistance;
	private Vector3 _smoothedSeatCamUp;
	private Angles _seatedAngles;
	private Vector3 _lastSeatWorldPos;
	private bool _wasThirdPerson;

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

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		UpdateSeatedCamera();
	}

	private void UpdateSeatedCamera()
	{
		var isThirdPerson = Controller.ThirdPerson;

		if ( isThirdPerson != _wasThirdPerson )
		{
			_wasThirdPerson = isThirdPerson;
			_cachedSeat = null;
			_smoothedDistance = 0;
			_smoothedSeatCamUp = Vector3.Up;
			_seatedAngles = (Angles)Scene.Camera.WorldRotation;
		}

		if ( !isThirdPerson ) return;

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
			_smoothedSeatCamUp = seatGo.WorldRotation.Up;
			_seatedAngles = Scene.Camera.WorldRotation.Angles();
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

		// Smooth up vector for contraption tilt/roll
		_smoothedSeatCamUp = Vector3.Lerp( _smoothedSeatCamUp, seatGo.WorldRotation.Up, Time.Delta * SeatedCameraRollSpeed ).Normal;

		// Compose rotation: yaw around world up, then pitch around local right, no gimbal lock
		var camRot = Rotation.FromYaw( _seatedAngles.yaw ) * Rotation.FromPitch( _seatedAngles.pitch );
		var camPos = seatPos + camRot.Backward * _smoothedDistance;

		Scene.Camera.WorldPosition = camPos;
		Scene.Camera.WorldRotation = Rotation.LookAt( seatPos - camPos, _smoothedSeatCamUp );
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
