using Sandbox.Utility;
using static Sandbox.Component;

public sealed class FreeCamGameObjectSystem : GameObjectSystem<FreeCamGameObjectSystem>, ISceneStage
{
	public bool IsActive { get; set; }

	Vector3 position;
	Vector3 smoothPosition;

	Angles angles;
	Angles smoothAngles;

	float fov = 80;
	float smoothFov = 80;

	public FreeCamGameObjectSystem( Scene scene ) : base( scene )
	{

	}

	void ISceneStage.Start()
	{
		if ( !IsActive )
			return;

		Input.Suppressed = true;

	}

	void ISceneStage.End()
	{
		if ( IsActive )
		{
			Input.Suppressed = false;
		}

		if ( Input.Keyboard.Pressed( "J" ) )
		{
			IsActive = !IsActive;

			if ( IsActive )
			{
				smoothPosition = Scene.Camera.WorldPosition;
				position = smoothPosition + Scene.Camera.WorldRotation.Backward * 50;
				angles = smoothAngles = Scene.Camera.WorldRotation;
				smoothFov = fov = Scene.Camera.FieldOfView;
				Scene.TimeScale = 0;

				Scene.Camera.RenderExcludeTags.Add( "firstperson" );
				Scene.Camera.RenderExcludeTags.Add( "ui" );
			}
			else
			{
				Scene.TimeScale = 1;
				Scene.Camera.RenderExcludeTags.Remove( "firstperson" );
				Scene.Camera.RenderExcludeTags.Remove( "ui" );
			}
		}

		if ( !IsActive )
			return;

		UpdateCameraPosition();
	}


	void UpdateCameraPosition()
	{
		var speed = 50;
		if ( Input.Down( "duck" ) ) speed = 5;
		if ( Input.Down( "run" ) ) speed = 500;

		if ( Input.Down( "attack2" ) )
		{
			fov += Input.MouseDelta.y * 0.1f;
			fov = fov.Clamp( 1, 120 );

		}
		else
		{
			angles += Input.AnalogLook * fov.Remap( 1, 100, 0.1f, 1 );
		}

		var velocity = angles.ToRotation() * Input.AnalogMove * speed;

		position += velocity * RealTime.SmoothDelta;

		smoothPosition = smoothPosition.LerpTo( position, RealTime.SmoothDelta * 3.0f );
		Scene.Camera.WorldPosition = smoothPosition;

		smoothAngles = smoothAngles.LerpTo( angles, RealTime.SmoothDelta * 5.0f );
		Scene.Camera.WorldRotation = smoothAngles + new Angles( Noise.Fbm( 2, Time.Now * 40, 1 ) * 2, Noise.Fbm( 2, Time.Now * 30, 60 ) * 2, 0 );

		smoothFov = smoothFov.LerpTo( fov, RealTime.SmoothDelta * 20.0f );
		Scene.Camera.FieldOfView = smoothFov;

		Scene.Camera.RenderExcludeTags.Remove( "viewer" );
	}
}
