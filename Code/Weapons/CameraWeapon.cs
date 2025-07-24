
using Sandbox.Rendering;
using Sandbox.Utility;

public class CameraWeapon : BaseWeapon
{
	float fov = 50;
	float roll = 0;

	DepthOfField dof;
	bool focusing;
	Vector3 focusPoint;

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

		var t = 20.0f;
		var s = 1.0f;

		var x = Noise.Perlin( Time.Now * t, 3, 5 ).Remap( 0, 1, -1, 1 ) * s;
		var y = Noise.Perlin( Time.Now * t * 0.8f, 3, 4 ).Remap( 0, 1, -1, 1 ) * s;

		camera.WorldRotation *= new Angles( x, y, 0 );

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
		}

		focusing = Input.Down( "attack1" );
	}

	private void UpdateDepthOfField( DepthOfField dof )
	{
		if ( !focusing )
		{
			dof.BlurSize = Scene.Camera.FieldOfView.Remap( 20, 80, 25, 5 );
			dof.FocusRange = 1024;
			dof.FrontBlur = false;

			var tr = Scene.Trace.Ray( Scene.Camera.Transform.World.ForwardRay, 5000 )
								.Radius( 8 )
								.IgnoreGameObjectHierarchy( GameObject.Root )
								.Run();

			focusPoint = tr.EndPosition;
		}

		var target = Scene.Camera.WorldPosition.Distance( focusPoint ) + 32;

		dof.FocalDistance = dof.FocalDistance.LerpTo( target, Time.Delta * 10.0f );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// nothing!
	}
}
