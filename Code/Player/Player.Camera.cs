using Sandbox.Movement;

public sealed partial class Player
{
	[Property, Group( "Camera" )] public float SeatedCameraDistance { get; set; } = 200f;
	[Property, Group( "Camera" )] public float SeatedCameraHeight { get; set; } = 40f;
	[Property, Group( "Camera" )] public float SeatedCameraPositionSpeed { get; set; } = 3f;
	[Property, Group( "Camera" )] public float SeatedCameraVelocityScale { get; set; } = 0.1f;

	private ISitTarget _cachedSeat;
	private bool _seatCameraInitialized;
	private float _minCameraDistance;
	private float _smoothedDistance;
	private Angles _seatedAngles;
	private Vector3 _lastSeatWorldPos;
	private List<BaseCarryable> _seatedWeapons;

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
		UpdateSeatedWeapons();
		ApplySeatedCameraSetup( camera );
		DrawSeatedWeaponHud();

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

	private void UpdateSeatedWeapons()
	{
		var seat = GetComponentInParent<ISitTarget>( false );
		if ( seat is null )
		{
			_cachedSeat = null;
			_seatedWeapons = null;
			return;
		}

		if ( seat != _cachedSeat )
		{
			_cachedSeat = seat;
			_seatCameraInitialized = false;
			RebuildSeatedWeapons( (seat as Component).GameObject );
		}
	}

	private void ApplySeatedCameraSetup( CameraComponent camera )
	{
		if ( !Controller.ThirdPerson )
			return;

		if ( _cachedSeat is null )
			return;

		var seatGo = (_cachedSeat as Component).GameObject;
		var seatPos = seatGo.WorldPosition + Vector3.Up * SeatedCameraHeight;

		if ( !_seatCameraInitialized )
		{
			_seatCameraInitialized = true;
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

	private void RebuildSeatedWeapons( GameObject seatGo )
	{
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( seatGo );

		_seatedWeapons ??= new List<BaseCarryable>();
		_seatedWeapons.Clear();

		foreach ( var obj in builder.Objects )
		{
			foreach ( var weapon in obj.GetComponentsInChildren<BaseCarryable>() )
			{
				if ( !weapon.HasOwner )
					_seatedWeapons.Add( weapon );
			}
		}
	}

	private void DrawSeatedWeaponHud()
	{
		if ( _seatedWeapons == null || _seatedWeapons.Count == 0 ) return;
		if ( Scene.Camera is null ) return;
		if ( Scene.Camera.RenderExcludeTags.Has( "ui" ) ) return;

		var hud = Scene.Camera.Hud;

		foreach ( var weapon in _seatedWeapons )
		{
			if ( !weapon.IsValid() ) continue;
			if ( weapon is IPlayerControllable controllable && !controllable.CanControl( Controller ) ) continue;

			Vector2 aimPos;

			if ( weapon.IsTargetedAim )
			{
				aimPos = Screen.Size * 0.5f;
			}
			else
			{
				var muzzle = weapon.MuzzleTransform.WorldTransform;
				var tr = Scene.Trace.Ray( muzzle.Position, muzzle.Position + muzzle.Rotation.Forward * 4096f )
					.IgnoreGameObjectHierarchy( weapon.GameObject.Root )
					.Run();

				aimPos = Scene.Camera.PointToScreenPixels( tr.EndPosition );
			}

			weapon.DrawHud( hud, aimPos );
		}
	}
}
