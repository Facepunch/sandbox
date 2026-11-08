public sealed class BuilderSnapMarkers
{
	static readonly Color Color = new( 0.2f, 0.6f, 1f );

	sealed class Lines : SceneDynamicObject
	{
		public Lines( SceneWorld world ) : base( world )
		{
			Transform = Transform.Zero;
			Material = Material.Load( "materials/gizmo/line.vmat" );
			Flags.CastShadows = false;
			Flags.IsOpaque = false;
			Flags.IsTranslucent = true;
			RenderLayer = SceneRenderLayer.OverlayWithoutDepth;
		}
	}

	Lines _lines;

	readonly List<BuilderPlane> _faces = new();
	Vector3 _min;
	Vector3 _max;

	public void Begin()
	{
		_faces.Clear();
		_min = new Vector3( float.MaxValue );
		_max = new Vector3( float.MinValue );
	}

	public void Add( SceneWorld world, in BuilderPlane plane, Vector3 eyes )
	{
		if ( !plane.HasFace ) return;

		foreach ( var face in _faces )
		{
			if ( IsSameFace( face, plane ) ) return;
		}

		if ( !_lines.IsValid() ) _lines = new Lines( world );
		if ( _faces.Count == 0 ) _lines.Init( Graphics.PrimitiveType.Lines );
		_faces.Add( plane );

		var extent = plane.FaceMax - plane.FaceMin;
		var lift = plane.Normal * 0.1f;

		for ( int i = 0; i < 3; i++ )
		for ( int j = 0; j < 3; j++ )
		{
			var point = plane.ToWorld( new Vector2( plane.FaceMin.x + extent.x * i * 0.5f, plane.FaceMin.y + extent.y * j * 0.5f ) ) + lift;

			var pixel = point.Distance( eyes ) * 0.0015f;
			var arm = MathF.Max( Math.Clamp( MathF.Min( extent.x, extent.y ) * 0.06f, 1f, 6f ), pixel * 8 );

			for ( int k = -1; k <= 1; k++ )
			{
				Segment( point - plane.Right * arm + plane.Forward * (k * pixel), point + plane.Right * arm + plane.Forward * (k * pixel) );
				Segment( point - plane.Forward * arm + plane.Right * (k * pixel), point + plane.Forward * arm + plane.Right * (k * pixel) );
			}

			_min = _min.ComponentMin( point - new Vector3( arm ) );
			_max = _max.ComponentMax( point + new Vector3( arm ) );
		}
	}

	static bool IsSameFace( in BuilderPlane a, in BuilderPlane b )
	{
		const float tolerance = 0.01f;
		return a.Origin.Distance( b.Origin ) < tolerance && Vector3.Dot( a.Normal, b.Normal ) > 0.9999f && Vector3.Dot( a.Right, b.Right ) > 0.9999f
			&& a.FaceMin.Distance( b.FaceMin ) < tolerance && a.FaceMax.Distance( b.FaceMax ) < tolerance;
	}

	public void End()
	{
		if ( _faces.Count == 0 )
		{
			Hide();
			return;
		}

		_lines.Bounds = new BBox( _min, _max );
		_lines.RenderingEnabled = true;
	}

	void Segment( Vector3 a, Vector3 b )
	{
		_lines.AddVertex( new Vertex( a, Color ) );
		_lines.AddVertex( new Vertex( b, Color ) );
	}

	public void Hide()
	{
		if ( _lines.IsValid() ) _lines.RenderingEnabled = false;
	}

	public void Destroy()
	{
		_lines?.Delete();
		_lines = null;
	}
}
