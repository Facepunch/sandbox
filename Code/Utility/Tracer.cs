public sealed class Tracer : Renderer, Component.ExecuteInEditor, Component.ITemporaryEffect
{
	[Header( "Position" )]
	[Property, Feature( "Tracer" )] public WorldPoint EndPoint { get; set; }

	[Header( "Speed" )]
	[Property, Feature( "Tracer" )] public float DistancePerSecond { get; set; } = 1.0f;
	[Property, Feature( "Tracer" )] public float Length { get; set; } = 100.0f;
	[Property, Feature( "Tracer" )] public float StartDistance { get; set; } = 200.0f;

	[Header( "Rendering" )]
	[Property, Feature( "Tracer" )] public Gradient LineColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0, Color.White ), new Gradient.ColorFrame( 1, Color.White.WithAlpha( 0 ) ) );
	[Property, Feature( "Tracer" )] public Curve LineWidth { get; set; } = new Curve( new Curve.Frame( 0, 2 ), new Curve.Frame( 1, 0 ) );
	[Property, Feature( "Tracer" )] public SceneLineObject.CapStyle StartCap { get; set; }
	[Property, Feature( "Tracer" )] public SceneLineObject.CapStyle EndCap { get; set; }

	[Group( "Rendering" )]
	[Property] public bool Opaque { get; set; } = true;

	[Group( "Rendering" )]
	[Property] public bool CastShadows { get; set; } = true;

	[Property, FeatureEnabled( "Light", Icon = "💡" )]
	public bool EnableLight { get; set; }

	[Property, Feature( "Light" )]
	public Gradient LightColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0, Color.White ), new Gradient.ColorFrame( 1, Color.White ) );

	[Property, Feature( "Light" )]
	public Curve LightRadius { get; set; } = new Curve( new Curve.Frame( 0, 128 ), new Curve.Frame( 0.5f, 256 ), new Curve.Frame( 1, 128 ) );

	[Property, Feature( "Light" ), Range( 0, 1 )]
	public float LightPosition { get; set; } = 0;

	bool ITemporaryEffect.IsActive => !_finished;

	float _distance = 0.0f;
	bool _finished = false;

	SceneLineObject _so;
	SceneLight _light;
	protected override void OnEnabled()
	{
		_so = new SceneLineObject( Scene.SceneWorld );
		_so.Transform = Transform.World;

		_distance = StartDistance;
	}

	protected override void OnDisabled()
	{
		_so?.Delete();
		_so = null;

		_light?.Delete();
		_light = null;
	}

	protected override void OnUpdate()
	{
		var len = WorldPosition.Distance( EndPoint.Get() );

		_distance += DistancePerSecond * Time.Delta;

		if ( _distance > len + Length )
		{
			_finished = true;

			if ( Scene.IsEditor )
				_distance = 0;
		}
	}

	protected override void OnPreRender()
	{
		if ( !_so.IsValid() )
			return;

		var travel = EndPoint.Get() - WorldPosition;
		var maxlen = travel.Length;

		if ( _distance - Length > maxlen )
		{
			_so.RenderingEnabled = false;

			_light?.Delete();
			_light = null;
			return;
		}

		var midDistance = (_distance - Length * 0.5f).Clamp( 0, maxlen );
		var delta = midDistance.LerpInverse( 0, maxlen );

		if ( EnableLight )
		{
			_light ??= new SceneLight( Scene.SceneWorld );
			_light.Transform = Transform.World;
			_light.QuadraticAttenuation = 10;
			_light.LightColor = LightColor.Evaluate( delta );
			_light.ShadowsEnabled = false;
			_light.Radius = LightRadius.Evaluate( delta );
			_light.Position = WorldPosition + travel.Normal * (_distance - Length * LightPosition).Clamp( 0, maxlen - 5 );
		}
		else
		{
			_light?.Delete();
			_light = null;
		}

		_so.StartCap = StartCap;
		_so.EndCap = EndCap;
		_so.Opaque = Opaque;

		_so.RenderingEnabled = true;
		_so.Transform = WorldTransform;
		_so.Flags.CastShadows = CastShadows;
		_so.Attributes.Set( "BaseTexture", Texture.White );
		_so.Attributes.SetCombo( "D_BLEND", Opaque ? 0 : 1 );

		_so.StartLine();

		for ( float x = 0; x <= 1.0f; x += 0.1f )
		{
			var s = (_distance - Length * x).Clamp( 0, maxlen );
			var lineStart = (_distance - maxlen).Clamp( 0, Length ) / Length;

			_so.AddLinePoint( WorldPosition + travel.Normal * s, LineColor.Evaluate( x ), LineWidth.Evaluate( x ) );
		}

		{
			var s = (_distance - Length).Clamp( 0, maxlen );
			var lineStart = (_distance).Clamp( 0, Length ) / Length;
		}

		_so.EndLine();
	}
}

public struct WorldPoint
{
	[KeyProperty]
	public Vector3 Origin { get; set; }
	public GameObject Parent { get; set; }

	public Vector3 Get()
	{
		if ( Parent.IsValid() )
			return Parent.WorldTransform.PointToWorld( Origin );

		return Origin;
	}

	public static implicit operator WorldPoint( Vector3 v )
	{
		return new WorldPoint { Origin = v };
	}
}
