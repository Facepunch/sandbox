
/// <summary>
/// Shakes nearby cameras with continuous angular noise while enabled - rumbling machinery, engines,
/// earthquakes. Strength falls off with distance, and optionally over time.
/// </summary>
[Alias( "EnvShake" )]
public sealed class EnvironmentShake : Component, Component.ITemporaryEffect
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

	Effect _effect;

	protected override void OnEnabled()
	{
		EndTime = TimeLength;
		_effect = CameraEffectSystem.Current?.Add( new Effect { Source = this, Duration = 0 } );
	}

	protected override void OnDisabled()
	{
		_effect?.Stop();
		_effect = null;
	}

	/// <summary>
	/// Perlin angular noise radiating from the component, faded by its distance and time curves.
	/// </summary>
	sealed class Effect : CameraEffectSystem.BaseEffect
	{
		public EnvironmentShake Source { get; init; }

		public override bool IsDone => base.IsDone || !Source.IsValid();

		public override float ScaleFor( CameraComponent camera )
		{
			if ( !Source.IsValid() )
				return 0f;

			var distanceDelta = (camera.WorldPosition.Distance( Source.WorldPosition ) / Source.MaxDistance).Clamp( 0f, 1f );
			var amount = Source.DistanceCurve.EvaluateDelta( distanceDelta );

			if ( Source.TimeLimited )
			{
				var timeDelta = (Source.EndTime.Relative / Source.TimeLength).Remap( 1, 0 );

				if ( timeDelta >= 1f )
					return 0f;

				amount *= Source.TimeCurve.Evaluate( timeDelta );
			}

			return amount;
		}

		public override void Evaluate( float scale, ref Vector3 position, ref Angles angles, ref float fieldOfView )
		{
			var noisex = Sandbox.Utility.Noise.Perlin( Time.Now * Source.Rate, 0 ).Remap( 0, 1, -1, 1 );
			var noisey = Sandbox.Utility.Noise.Perlin( Time.Now * Source.Rate + 830, 0 ).Remap( 0, 1, -1, 1 );
			var noisez = Sandbox.Utility.Noise.Perlin( Time.Now * Source.Rate + 340, 0 ).Remap( 0, 1, -1, 1 );

			angles += new Angles( noisex * Source.Scale.pitch, noisey * Source.Scale.yaw, noisez * Source.Scale.roll ) * scale;
		}
	}
}
