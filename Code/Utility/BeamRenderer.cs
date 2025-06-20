using Sandbox.Utility;

public sealed class BeamRenderer : Renderer, Component.ExecuteInEditor, Component.ITemporaryEffect
{
	[Header( "Position" )]
	[Property] public WorldPoint EndPoint { get; set; }

	[Header( "Rendering" )]
	[Property] public Gradient LineColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0, Color.White ), new Gradient.ColorFrame( 1, Color.White.WithAlpha( 0 ) ) );
	[Property] public Curve LineWidth { get; set; } = new Curve( new Curve.Frame( 0, 2 ), new Curve.Frame( 1, 0 ) );
	[Property] public float LineScale { get; set; } = 1;
	[Property] public SceneLineObject.CapStyle StartCap { get; set; }
	[Property] public SceneLineObject.CapStyle EndCap { get; set; }

	[Property] public float SectionLength { get; set; } = 1.0f;

	[Header( "Randomness" )]
	[Property, Feature( "Noise" )] public ParticleFloat RandomAmount { get; set; }
	[Property, Feature( "Noise" )] public float RandomScale { get; set; } = 100;
	[Property, Feature( "Noise" )] public float RandomSpeed { get; set; } = 10;

	[Header( "Life Time" )]
	[Property] public ParticleFloat LifeLength { get; set; } = 1;

	[Group( "Rendering" )]
	[Property] public bool Opaque { get; set; } = true;

	[Group( "Rendering" )]
	[Property] public bool CastShadows { get; set; } = true;

	bool ITemporaryEffect.IsActive => dieTime > 0;

	TimeUntil dieTime;

	SceneLineObject _so;

	protected override void OnEnabled()
	{
		_so = new SceneLineObject( Scene.SceneWorld );
		_so.Transform = Transform.World;

		dieTime = LifeLength.Evaluate( 0.5f, Random.Shared.Float() );
	}

	protected override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
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

		// Generate points along the curve
		int segments = Math.Max( 2, (int)(maxlen / SectionLength) );
		for ( int i = 0; i <= segments; i++ )
		{
			float t = i / (float)segments;
			var bezierPoint = Vector3.Lerp( WorldPosition, endPoint, t );
			AddPoint( t, t * maxlen, bezierPoint, up, left );
		}

		_so.EndLine();

		RenderOptions.Apply( _so );
	}

	void AddPoint( float delta, float beampos, Vector3 position, in Vector3 up, in Vector3 left )
	{
		var rand = RandomAmount.Evaluate( delta, Random.Shared.Float() );

		var r = Noise.FbmVector( 1, Time.Now * RandomSpeed + delta * rand ) * RandomScale;

		position += r.x * left;
		position += r.y * up;

		_so.AddLinePoint( position, LineColor.Evaluate( delta ), LineWidth.Evaluate( delta ) * LineScale );
	}
}
