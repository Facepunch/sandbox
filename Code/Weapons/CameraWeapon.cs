using Sandbox.Rendering;

public class CameraWeapon : BaseWeapon
{
	float fov = 50;
	float roll = 0;

	DepthOfField dof;
	bool focusing;
	Vector3 focusPoint;

	[Property] SoundEvent CameraShoot { get; set; }

	public override bool WantsHideHud => true;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		dof = Scene.Camera.Components.GetOrCreate<DepthOfField>();
		dof.Flags |= ComponentFlags.NotNetworked;

		focusing = false;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( IsProxy )
			return;

		dof?.Destroy();
		dof = default;
	}

	/// <summary>
	/// We want to control the camera fov
	/// </summary>
	public override void OnCameraSetup( Player player, Sandbox.CameraComponent camera )
	{
		//Log.Info( $"{player.Network.IsOwner} {Network.IsOwner}" );
		if ( !player.Network.IsOwner || !Network.IsOwner ) return;

		camera.FieldOfView = fov;
		camera.WorldRotation = camera.WorldRotation * new Angles( 0, 0, roll );
	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		// We're zooming
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

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// nothing!
	}
}
