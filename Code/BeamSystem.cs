using System.Runtime.CompilerServices;

namespace Sandbox;

public class BeamSystem : Component, Component.ExecuteInEditor, Component.ITemporaryEffect
{
	[Property, Range( 0, 50 )] public float DepthFeather { get; set; } = 0.0f;
	[Property] public ParticleFloat Scale { get; set; } = 1.0f;

	[Header( "Render Properties" )]
	[Feature( "Rendering" ), Property] public bool Additive { get; set; }
	[Feature( "Rendering" ), Property] public bool Shadows { get; set; }
	[Feature( "Rendering" ), Property] public bool Lighting { get; set; }
	[Feature( "Rendering" ), Property] public bool Opaque { get; set; }

	[Header( "Target" )]
	[Property] public Vector3 TargetPosition { get; set; }
	[Property] public GameObject TargetGameObject { get; set; }
	[Property] public Vector3 TargetRandom { get; set; }
	[Property] public bool FollowPoints { get; set; } = true;

	[Header( "Spawning" )]
	[Property] public float BeamsPerSecond { get; set; } = 5;
	[Property] public int MaxBeams { get; set; } = 20;
	[Property] public int InitialBurst { get; set; } = 5;
	[Property] public ParticleFloat BeamLifetime { get; set; } = 0.5f;

	[Header( "Texture" )]
	[Property] public Texture Texture { get; set; }
	[Property] public ParticleFloat TextureOffset { get; set; } = 1.0f;
	[Property] public ParticleFloat TextureScale { get; set; } = 128;
	[Property] public ParticleFloat TextureScrollSpeed { get; set; } = 0.0f;

	[Header( "Color" )]
	[Feature( "Rendering" ), Property] public ParticleGradient BeamColor { get; set; } = new Color( 1, 1, 1, 1 );
	[Feature( "Rendering" ), Property] public ParticleFloat Alpha { get; set; } = 1.0f;
	[Feature( "Rendering" ), Property] public ParticleFloat Brightness { get; set; } = 1.0f;

	[FeatureEnabled( "Travel" )]
	[Property] public bool TravelBetweenPoints { get; set; } = true;
	[Property, Feature( "Travel" )] public ParticleFloat TravelLerp { get; set; } = new ParticleFloat { Evaluation = ParticleFloat.EvaluationType.Life, Type = ParticleFloat.ValueType.Curve, CurveA = new Curve( new( 0, 0 ), new( 1, 1 ) ) };
	[Property, Feature( "Travel" )] public ParticleFloat BeamLength { get; set; }

	bool ITemporaryEffect.IsActive => _beams.Count > 0;

	List<Beam> _beams = new();
	float _timeSinceLastSpawn;

	public class Beam
	{
		public Vector3 StartPosition;
		public Vector3 EndPosition;
		public LineRenderer Renderer;

		public float TimeBorn;
		public float TimeDie;

		public float Delta => (Time.Now - TimeBorn) / (TimeDie - TimeBorn);
		public int RandomSeed;

		public void Destroy()
		{
			if ( Renderer.IsValid() )
			{
				Renderer.Destroy();
				Renderer = null;
			}
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public float Rand( int seed = 0, [CallerLineNumber] int line = 0 )
		{
			int i = RandomSeed + (line * 20) + seed;
			return Game.Random.FloatDeterministic( i );
		}
	}

	protected override void OnEnabled()
	{
		for ( int i = 0; i < InitialBurst; i++ )
		{
			SpawnBeam();
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		foreach ( var beam in _beams )
		{
			beam.Destroy();
		}

		_beams.Clear();
	}

	protected override void OnUpdate()
	{
		_beams ??= new();

		_timeSinceLastSpawn += Time.Delta;

		float interval = 1f / BeamsPerSecond;
		while ( _timeSinceLastSpawn >= interval )
		{
			_timeSinceLastSpawn -= interval;

			if ( _beams.Count < MaxBeams )
			{
				SpawnBeam();
			}
		}

		for ( int i = _beams.Count - 1; i >= 0; i-- )
		{
			var beam = _beams[i];
			if ( Time.Now >= beam.TimeDie )
			{
				DestroyBeam( beam );
				continue;
			}

			UpdateBeam( beam );
		}

		if ( Scene.IsEditor && BeamsPerSecond == 0 && _beams.Count == 0 )
		{
			OnEnabled();
		}
	}

	static int seed = 0;

	Beam CreateBeam()
	{
		var beam = new Beam
		{
			Renderer = AddComponent<LineRenderer>(),
			TimeBorn = Time.Now,
			RandomSeed = seed++
		};

		beam.TimeDie = Time.Now + BeamLifetime.Evaluate( 0.5f, beam.Rand( 33 ) );

		beam.Renderer.Flags |= ComponentFlags.NotSaved | ComponentFlags.NotEditable | ComponentFlags.Hidden;
		_beams.Add( beam );
		return beam;
	}

	public void SpawnBeam()
	{
		var beam = CreateBeam();
		UpdatePositions( beam );
	}

	void DestroyBeam( Beam beam )
	{
		_beams.Remove( beam );
		beam.Destroy();
	}

	void UpdatePositions( Beam beam )
	{
		beam.StartPosition = WorldPosition;
		beam.EndPosition = TargetPosition;
		if ( TargetGameObject.IsValid() ) beam.EndPosition = TargetGameObject.WorldPosition;

		beam.EndPosition += Vector3.Random * TargetRandom;
	}

	void UpdateBeam( Beam beam )
	{
		var lineRenderer = beam.Renderer;
		var lifeDelta = beam.Delta;

		if ( FollowPoints )
		{
			UpdatePositions( beam );
		}

		var texScale = TextureScale.Evaluate( lifeDelta, beam.Rand( 55 ) );
		if ( texScale == 0 ) texScale = 0.01f;

		var color = BeamColor.Evaluate( lifeDelta, beam.Rand( 2 ) );
		color = color.WithAlphaMultiplied( Alpha.Evaluate( lifeDelta, beam.Rand( 88 ) ) );
		color = color.WithColorMultiplied( Brightness.Evaluate( lifeDelta, beam.Rand( 11 ) ) );

		lineRenderer.UseVectorPoints = true;
		lineRenderer.VectorPoints ??= new();
		lineRenderer.VectorPoints.Clear();
		lineRenderer.Width = Scale.Evaluate( lifeDelta, beam.Rand( 98 ) );
		lineRenderer.Color = color;
		lineRenderer.Additive = Additive;
		lineRenderer.Opaque = Opaque;
		lineRenderer.CastShadows = Shadows;
		lineRenderer.Lighting = Lighting;
		lineRenderer.DepthFeather = DepthFeather;
		lineRenderer.Texturing = new TrailTextureConfig
		{
			Texture = Texture,
			UnitsPerTexture = texScale,
			Offset = TextureOffset.Evaluate( lifeDelta, beam.Rand( 23 ) ) + Time.Now * TextureScrollSpeed.Evaluate( lifeDelta, beam.Rand( 32 ) ) / texScale,
			Clamp = false,
			WorldSpace = true,
		};

		if ( TravelBetweenPoints )
		{
			var lerp = TravelLerp.Evaluate( lifeDelta, beam.Rand( 3289 ) );
			var length = beam.StartPosition.Distance( beam.EndPosition );
			var chunklength = BeamLength.Evaluate( lifeDelta, beam.Rand( 44 ) );

			lineRenderer.VectorPoints.Add( beam.StartPosition );
			lineRenderer.VectorPoints.Add( beam.EndPosition );

			var chunksPerLength = length / chunklength;
			chunksPerLength += 1;

			var offset = 1 + (lerp * -chunksPerLength);

			lineRenderer.Texturing = lineRenderer.Texturing with
			{
				Clamp = true,
				UnitsPerTexture = chunklength,
				Offset = offset
			};

		}
		else
		{
			lineRenderer.VectorPoints.Add( beam.StartPosition );
			lineRenderer.VectorPoints.Add( beam.EndPosition );
		}
	}
}
