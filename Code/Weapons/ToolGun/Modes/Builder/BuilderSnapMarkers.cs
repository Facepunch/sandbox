/// <summary>
/// Bold blue crosses on a face's snap points: its corners, the middles of its edges and its centre. All of them are one
/// line-list scene object the Builder tool rebuilds each frame, rather than dozens of debug overlay objects.
/// </summary>
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

	/// <summary>
	/// Draw the markers for <paramref name="plane"/>'s face, sized for someone looking from <paramref name="eyes"/>.
	/// </summary>
	public void Show( SceneWorld world, in BuilderPlane plane, Vector3 eyes )
	{
		if ( !plane.HasFace )
		{
			Hide();
			return;
		}

		if ( !_lines.IsValid() ) _lines = new Lines( world );

		var extent = plane.FaceMax - plane.FaceMin;
		var lift = plane.Normal * 0.1f;
		var min = new Vector3( float.MaxValue );
		var max = new Vector3( float.MinValue );

		_lines.Init( Graphics.PrimitiveType.Lines );

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

			min = min.ComponentMin( point - new Vector3( arm ) );
			max = max.ComponentMax( point + new Vector3( arm ) );
		}

		_lines.Bounds = new BBox( min, max );
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
