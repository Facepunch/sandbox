using Sandbox.Rendering;

public class CameraWeapon : BaseWeapon
{
	float fov = 50;
	float roll = 0;

	DepthOfField dof;
	bool focusing;
	Vector3 focusPoint;

	[Property] SoundEvent CameraShoot { get; set; }

	/// <summary>
	/// The RT camera's resolution 
	/// </summary>
	private static int _cameraResolution = 512;

	/// <summary>
	/// The render target texture produced by this camera. Read by <see cref="TVEntity"/>.
	/// </summary>
	public Texture RenderTexture => _renderTexture;

	private Texture _renderTexture;
	private CameraComponent _rtCamera;

	public override bool WantsHideHud => true;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		EnsureRTCamera();
		EnsureRenderTexture();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		DestroyDepthOfField();
		CleanupRenderTexture();
		CleanupRTCamera();
	}

	protected override void OnDestroy()
	{
		DestroyDepthOfField();
		CleanupRenderTexture();
		CleanupRTCamera();
		base.OnDestroy();
	}

	protected override void OnPreRender()
	{
		if ( _rtCamera is null ) return;

		EnsureRenderTexture();

		if ( HasOwner && Scene.Camera is not null )
		{
			// When held, mirror the player's camera so the TV shows their POV.
			_rtCamera.WorldPosition = Scene.Camera.WorldPosition;
			_rtCamera.WorldRotation = Scene.Camera.WorldRotation;
			_rtCamera.FieldOfView = Scene.Camera.FieldOfView;

			if ( !_rtCamera.RenderExcludeTags.Has( "viewer" ) )
				_rtCamera.RenderExcludeTags.Add( "viewer" );
		}
		else
		{
			_rtCamera.RenderExcludeTags.Remove( "viewer" );
			_rtCamera.FieldOfView = 40f;
		}
	}

	/// <summary>
	/// We want to control the camera fov when held by a player.
	/// </summary>
	public override void OnCameraSetup( Player player, Sandbox.CameraComponent camera )
	{
		if ( !player.Network.IsOwner || !Network.IsOwner ) return;

		camera.FieldOfView = fov;
		camera.WorldRotation = camera.WorldRotation * new Angles( 0, 0, roll );
	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		if ( Input.Down( "attack2" ) )
		{
			angles = default;
		}

		float sensitivity = fov.Remap( 1, 70, 0.01f, 1 );
		angles *= sensitivity;
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Pressed( "reload" ) )
		{
			fov = 50;
			roll = 0;
		}

		if ( Input.Down( "attack2" ) )
		{
			fov += Input.AnalogLook.pitch;
			fov = fov.Clamp( 1, 150 );
			roll -= Input.AnalogLook.yaw;
		}

		EnsureDepthOfField();

		if ( dof.IsValid() )
		{
			UpdateDepthOfField( dof );
		}

		if ( focusing && Input.Released( "attack1" ) )
		{
			Game.TakeScreenshot();
			Sandbox.Services.Stats.Increment( "photos", 1 );

			GameObject?.PlaySound( CameraShoot );
		}

		focusing = Input.Down( "attack1" );
	}

	private void EnsureDepthOfField()
	{
		if ( dof.IsValid() ) return;

		dof = Scene.Camera.GetOrAddComponent<DepthOfField>();
		dof.Flags |= ComponentFlags.NotNetworked;
		focusing = false;
	}

	private void DestroyDepthOfField()
	{
		dof?.Destroy();
		dof = default;
	}

	private void UpdateDepthOfField( DepthOfField dof )
	{
		if ( !focusing )
		{
			dof.BlurSize = MathF.Pow( Scene.Camera.FieldOfView.Remap( 1, 55, 1, 0 ), 4 ) * 16;
			dof.FocusRange = 512;
			dof.FrontBlur = false;

			var tr = Scene.Trace.Ray( Scene.Camera.Transform.World.ForwardRay, 5000 )
								.Radius( 4 )
								.IgnoreGameObjectHierarchy( GameObject.Root )
								.Run();

			focusPoint = tr.EndPosition;
		}

		var target = Scene.Camera.WorldPosition.Distance( focusPoint ) + 64;

		dof.FocalDistance = dof.FocalDistance.LerpTo( target, Time.Delta * 2.0f );
	}

	private void EnsureRTCamera()
	{
		_rtCamera = GetComponentInChildren<CameraComponent>( true );

		if ( _rtCamera is null )
		{
			var go = new GameObject( GameObject, true, "rt_camera" );
			_rtCamera = go.AddComponent<CameraComponent>();
		}

		_rtCamera.IsMainCamera = false;
		_rtCamera.BackgroundColor = Color.Black;
		_rtCamera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;
		_rtCamera.FieldOfView = fov;
		_rtCamera.RenderExcludeTags.Add( "viewmodel" );
	}

	private void EnsureRenderTexture()
	{
		if ( _renderTexture is not null && _renderTexture.Width == _cameraResolution && _renderTexture.Height == _cameraResolution )
			return;

		CleanupRenderTexture();

		_renderTexture = Texture.CreateRenderTarget()
			.WithSize( _cameraResolution, _cameraResolution )
			.Create();

		if ( _rtCamera is not null )
			_rtCamera.RenderTarget = _renderTexture;
	}

	private void CleanupRenderTexture()
	{
		if ( _rtCamera is not null )
			_rtCamera.RenderTarget = null;

		_renderTexture?.Dispose();
		_renderTexture = null;
	}

	private void CleanupRTCamera()
	{
		_rtCamera = null;
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// nothing!
	}
}
