
[Alias( "EnvShake" )]
public sealed class EnvironmentShake : Component, Component.ITemporaryEffect, ICameraSetup
{
	[Property] public float Rate { get; set; } = 10.0f;
	[Property] public Angles Scale { get; set; } = new Angles( 100, 100, 0 );
	[Property] public float MaxDistance { get; set; } = 1024;
	[Property] public Curve DistanceCurve { get; set; } = new Curve( new( 0, 1 ), new( 1, 0 ) );
	[FeatureEnabled( "Timed" ), Property] public bool TimeLimited { get; set; } = false;
	[Feature( "Timed" ), Property] public float TimeLength { get; set; } = 5;
	[Feature( "Timed" ), Property] public Curve TimeCurve { get; set; } = new Curve( new( 0, 1 ), new( 1, 0 ) );
	[Sync] public TimeUntil EndTime { get; set; }

	public bool IsActive => !TimeLimited || EndTime > 0;

	protected override void OnEnabled()
	{
		EndTime = TimeLength;
	}

	void ICameraSetup.PostSetup( CameraComponent camera )
	{
		var distance = camera.WorldPosition.Distance( WorldPosition );
		var distanceDelta = (distance / MaxDistance).Clamp( 0, 1 );

		var amount = DistanceCurve.EvaluateDelta( distanceDelta ) * GamePreferences.Screenshake;

		var noisex = Sandbox.Utility.Noise.Perlin( Time.Now * Rate, 0 ).Remap( 0, 1, -1, 1 );
		var noisey = Sandbox.Utility.Noise.Perlin( Time.Now * Rate + 830, 0 ).Remap( 0, 1, -1, 1 );
		var noisez = Sandbox.Utility.Noise.Perlin( Time.Now * Rate + 340, 0 ).Remap( 0, 1, -1, 1 );

		if ( TimeLimited )
		{
			var timeDelta = (EndTime.Relative / TimeLength).Remap( 1, 0 );

			if ( timeDelta >= 1 )
				return;

			amount *= TimeCurve.Evaluate( timeDelta );
		}

		camera.LocalRotation *= Rotation.From( noisex * Scale.pitch, noisey * Scale.yaw, noisez * Scale.roll ) * amount;
	}
}
