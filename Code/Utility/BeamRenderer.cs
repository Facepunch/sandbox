public sealed class BeamRenderer : Renderer, Component.ExecuteInEditor, Component.ITemporaryEffect
{
	[Header( "Position" )]
	[Property] public WorldPoint EndPoint { get; set; }

	[Header( "Position" )]
	[Property] public WorldPoint? MiddlePoint { get; set; }

	[Header( "Rendering" )]
	[Property] public Gradient LineColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0, Color.White ), new Gradient.ColorFrame( 1, Color.White.WithAlpha( 0 ) ) );
	[Property] public Curve LineWidth { get; set; } = new Curve( new Curve.Frame( 0, 2 ), new Curve.Frame( 1, 0 ) );
	[Property] public SceneLineObject.CapStyle StartCap { get; set; }
	[Property] public SceneLineObject.CapStyle EndCap { get; set; }

	[Property] public float SectionLength { get; set; } = 1.0f;

	[Header( "Randomness" )]
	[Property, Feature( "Noise" )] public ParticleFloat RandomAmount { get; set; }
	[Property, Feature( "Noise" )] public float RandomScale { get; set; } = 100;

	[Header( "Wave" )]
	[Property, Feature( "Noise" )] public ParticleFloat SinFrequency { get; set; } = 0.5f;
	[Property, Feature( "Noise" )] public ParticleFloat SinAmplitude { get; set; } = 10;
	[Property, Feature( "Noise" )] public ParticleFloat SinSpeed { get; set; } = 10;

	[Property, Feature( "Noise" )] public ParticleFloat CosFrequency { get; set; } = 0.5f;
	[Property, Feature( "Noise" )] public ParticleFloat CosAmplitude { get; set; } = 10;
	[Property, Feature( "Noise" )] public ParticleFloat CosSpeed { get; set; } = 10;

	[Group( "Rendering" )]
	[Property] public bool Opaque { get; set; } = true;

	[Group( "Rendering" )]
	[Property] public bool CastShadows { get; set; } = true;

	bool ITemporaryEffect.IsActive => !_finished;

	bool _finished = false;

	SceneLineObject _so;
	SceneLight _light;

	protected override void OnEnabled()
	{
		_so = new SceneLineObject( Scene.SceneWorld );
		_so.Transform = Transform.World;
	}

	protected override void OnDisabled()
	{
		_so?.Delete();
		_so = null;

		_light?.Delete();
		_light = null;
	}

	protected override void OnPreRender()
	{
		if ( !_so.IsValid() )
			return;

		if ( SectionLength < 0.1f )
			SectionLength = 0.1f;

		var endPoint = EndPoint.Get();
		var travel = endPoint - WorldPosition;
		var maxlen = travel.Length;

		_so.StartCap = StartCap;
		_so.EndCap = EndCap;
		_so.Opaque = Opaque;

		_so.RenderingEnabled = true;
		_so.Transform = WorldTransform;
		_so.Flags.CastShadows = CastShadows;
		_so.Attributes.Set( "BaseTexture", Texture.White );
		_so.Attributes.SetCombo( "D_BLEND", Opaque ? 0 : 1 );

		_so.StartLine();

		var rot = Rotation.LookAt( travel );
		var up = rot.Up;
		var left = rot.Left;

		var midPoint = (WorldPosition + endPoint) * 0.5f;
		if ( MiddlePoint.HasValue )
		{
			midPoint = MiddlePoint.Value.Get();
		}

		// Generate points along the curve
		int segments = Math.Max( 2, (int)(maxlen / SectionLength) );
		for ( int i = 0; i <= segments; i++ )
		{
			float t = i / (float)segments;
			var bezierPoint = Vector3.CubicBezier( WorldPosition, endPoint, midPoint, midPoint, t );
			AddPoint( t, t * maxlen, bezierPoint, up, left );
		}

		_so.EndLine();

		RenderOptions.Apply( _so );
	}

	void AddPoint( float delta, float beampos, Vector3 position, in Vector3 up, in Vector3 left )
	{
		var rand = RandomAmount.Evaluate( delta, Random.Shared.Float() ) * RandomScale;
		position += Vector3.Random * rand;

		{
			var wave = MathF.Sin( (beampos * SinFrequency.Evaluate( delta, Random.Shared.Float() )) + Time.Now * SinSpeed.Evaluate( delta, Random.Shared.Float() ) ) * SinAmplitude.Evaluate( delta, Random.Shared.Float() );
			position += wave * up;
		}

		{
			var wave = MathF.Cos( (beampos * CosFrequency.Evaluate( delta, Random.Shared.Float() )) + Time.Now * CosSpeed.Evaluate( delta, Random.Shared.Float() ) ) * CosAmplitude.Evaluate( delta, Random.Shared.Float() );
			position += wave * left;
		}

		_so.AddLinePoint( position, LineColor.Evaluate( delta ), LineWidth.Evaluate( delta ) );
	}
}
