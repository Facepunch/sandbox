namespace Sandbox;

/// <summary>Render surfaces and convex compound collision, including genuinely hollow pieces.</summary>
public sealed class BuilderGeometry
{
	static readonly Dictionary<string, Model> Cache = new();
	// Bump when recipes change so hotload cannot reuse old meshes/collision.
	public const int GeometryVersion = 3;
	readonly List<Vertex> vertices = new();
	readonly List<int> indices = new();
	readonly ModelBuilder builder = Model.Builder;
	public static Model GetModel( BuilderCatalog.Piece piece )
	{
		var cacheKey = $"{GeometryVersion}:{piece.Id}";
		if ( Cache.TryGetValue( cacheKey, out var model ) && float.IsFinite( model.Bounds.Size.Length ) ) return model;
		var geometry = new BuilderGeometry();
		geometry.Build( piece );
		var mesh = new Mesh( Material.Load( BuilderCatalog.DefaultMaterial ) );
		mesh.CreateVertexBuffer( geometry.vertices.Count, geometry.vertices );
		mesh.CreateIndexBuffer( geometry.indices.Count, geometry.indices );
		var bounds = BBox.FromPoints( geometry.vertices.Select( v => v.Position ) );
		mesh.Bounds = bounds;
		model = geometry.builder.AddMesh( mesh ).WithHullBounds( bounds ).WithViewBounds( bounds )
			.WithName( $"builder/{piece.Id}" ).WithSurface( "plastic" ).Create();
		Cache[cacheKey] = model;
		return model;
	}

	void Triangle( Vector3 a, Vector3 b, Vector3 c )
	{
		var cross = Vector3.Cross( b - a, c - a );
		if ( cross.LengthSquared < 0.000001f ) return;
		var normal = cross.Normal;
		// Stable planar axes keep the two triangles of a face on the same UV projection.
		var tangent = MathF.Abs( normal.z ) > 0.99f ? Vector3.Forward : Vector3.Cross( Vector3.Up, normal ).Normal;
		var bitangent = Vector3.Cross( normal, tangent );
		foreach ( var p in new[] { a, b, c } )
		{
			indices.Add( vertices.Count );
			vertices.Add( new Vertex( p, normal, tangent, new Vector4( Vector3.Dot( p, tangent ) / 48, Vector3.Dot( p, bitangent ) / 48, 0, 0 ) ) );
		}
	}
	void Quad( Vector3 a, Vector3 b, Vector3 c, Vector3 d ) { Triangle( a, b, c ); Triangle( a, c, d ); }

	// Convex counterclockwise XY polygon extruded along Z.
	void Prism( Vector2[] polygon, float bottom, float top )
	{
		Vector3 P( int i, float z ) => new( polygon[i].x, polygon[i].y, z );
		for ( int i = 1; i < polygon.Length - 1; i++ )
		{
			Triangle( P( 0, top ), P( i, top ), P( i + 1, top ) );
			Triangle( P( 0, bottom ), P( i + 1, bottom ), P( i, bottom ) );
		}
		for ( int i = 0; i < polygon.Length; i++ )
		{
			int j = (i + 1) % polygon.Length;
			Quad( P( i, bottom ), P( j, bottom ), P( j, top ), P( i, top ) );
		}
		builder.AddCollisionHull( polygon.Select( p => new Vector3( p.x, p.y, bottom ) ).Concat( polygon.Select( p => new Vector3( p.x, p.y, top ) ) ).ToList() );
	}

	// A hollow parametric solid. Each surface cell contributes its own convex hull;
	// never use a single convex hull for a tube/dome, as that would seal the opening.
	void Hollow( Func<float, float, bool, Vector3> point, int columns, int rows, bool closed )
	{
		for ( int u = 0; u < columns; u++ )
		for ( int v = 0; v < rows; v++ )
		{
			float a = (float)u / columns, b = (float)(u + 1) / columns;
			float c = (float)v / rows, d = (float)(v + 1) / rows;
			var p = new[] { point(a,c,false), point(b,c,false), point(b,d,false), point(a,d,false),
				point(a,c,true), point(b,c,true), point(b,d,true), point(a,d,true) };
			Quad( p[0], p[1], p[2], p[3] );
			Quad( p[7], p[6], p[5], p[4] );
			if ( v == 0 ) Quad( p[4], p[5], p[1], p[0] );
			if ( v == rows - 1 ) Quad( p[3], p[2], p[6], p[7] );
			if ( !closed && u == 0 ) Quad( p[0], p[3], p[7], p[4] );
			if ( !closed && u == columns - 1 ) Quad( p[5], p[6], p[2], p[1] );
			builder.AddCollisionHull( p.ToList() );
		}
	}

	void Lathe( float radius, float height, bool sphere, bool inverted = false, float offset = 0, bool cap = true )
	{
		var points = new List<Vector3>();
		int rows = sphere ? 16 : 1;
		Vector3 Point( int u, int v )
		{
			float a = u * MathF.Tau / 32, t = (float)v / rows;
			float r = sphere ? MathF.Sin( t * MathF.PI ) * radius : radius * (inverted ? t : 1 - t);
			float z = sphere ? -MathF.Cos( t * MathF.PI ) * height / 2 : (t - 0.5f) * height;
			return new Vector3( MathF.Cos(a) * r, MathF.Sin(a) * r, z + offset );
		}
		for ( int u = 0; u < 32; u++ )
		{
			for ( int v = 0; v < rows; v++ )
			{
				var a = Point(u,v); var b = Point(u+1,v); var c = Point(u+1,v+1); var d = Point(u,v+1);
				Quad( a,b,c,d );
				if ( !sphere ) points.AddRange( [a,b,c,d] );
			}
			if ( !sphere && cap )
			{
				int end = inverted ? 1 : 0;
				var center = new Vector3( 0, 0, offset + (end - 0.5f) * height );
				if ( inverted ) Triangle( center, Point(u,end), Point(u+1,end) );
				else Triangle( center, Point(u+1,end), Point(u,end) );
			}
		}
		// Dense render spheres can fail convex-hull cooking while still producing
		// a physics part. Catalog spheres are round, so use an exact native sphere.
		if ( sphere )
			builder.AddCollisionSphere( radius, new Vector3( 0, 0, offset ) );
		else
			builder.AddCollisionHull( points.Distinct().ToList() );
	}

	void Build( BuilderCatalog.Piece piece )
	{
		var size = piece.Size * BuilderCatalog.Unit;
		float x = size.x / 2, y = size.y / 2, z = size.z / 2;
		float wall = MathF.Min( 6, x * 0.25f );
		Vector2[] rectangle = [new(-x,-y), new(x,-y), new(x,y), new(-x,y)];
		switch ( piece.Kind )
		{
			case BuilderCatalog.Shape.Box: Prism( rectangle, -z, z ); break;
			case BuilderCatalog.Shape.Triangle: Prism( [new(-x,-y),new(x,-y),new(-x,y)], -z, z ); break;
			case BuilderCatalog.Shape.Trapezium: Prism( [new(-x,-y),new(x,-y),new(x/2,y),new(-x/2,y)], -z, z ); break;
			case BuilderCatalog.Shape.Parallelogram: Prism( [new(-x,-y),new(x/2,-y),new(x,y),new(-x/2,y)], -z, z ); break;
			case BuilderCatalog.Shape.Pentagon:
				Prism( Enumerable.Range(0,5).Select( i => new Vector2( MathF.Cos(i*MathF.Tau/5)*x, MathF.Sin(i*MathF.Tau/5)*y ) ).ToArray(), -z,z ); break;
			case BuilderCatalog.Shape.Carved:
				// A triangular frame with quarter-unit walls and a real triangular opening.
				float w = 12;
				Vector2[] outer = [new(-x,-y),new(x,-y),new(-x,y)];
				Vector2[] inner = [new(-x+w,-y+w),new(x-w*(1+MathF.Sqrt(2)),-y+w),new(-x+w,y-w*(1+MathF.Sqrt(2)))];
				for ( int i = 0; i < 3; i++ ) { int j = (i+1)%3; Prism( [outer[i],outer[j],inner[j],inner[i]], -z,z ); }
				break;
			case BuilderCatalog.Shape.Stairs:
				int steps = Math.Max( 2, (int)MathF.Ceiling( piece.Size.x * 4 ) );
				for ( int i = 0; i < steps; i++ )
				{
					float a = -x + size.x*i/steps, b = -x + size.x*(i+1)/steps;
					Prism( [new(a,-y),new(b,-y),new(b,y),new(a,y)], -z, -z+size.z*(i+1)/steps );
				}
				break;
			case BuilderCatalog.Shape.Cone: Lathe( x,size.z,false ); break;
			case BuilderCatalog.Shape.MirroredCone:
				// Base-to-base: widest at the middle, points at the two ends.
				Lathe( x, z, false, true, -z / 2, cap: false );
				Lathe( x, z, false, false, z / 2, cap: false );
				break;
			case BuilderCatalog.Shape.Ring:
				// The legacy recipe enum/ident is retained for saves, but circle pieces
				// are solid discs with solid collision, not annular tubes.
				Prism( Enumerable.Range( 0, 32 ).Select( i => new Vector2(
					MathF.Cos( i * MathF.Tau / 32 ) * x, MathF.Sin( i * MathF.Tau / 32 ) * y ) ).ToArray(), -z, z );
				break;
			case BuilderCatalog.Shape.Sphere: Lathe( x,size.z,true ); break;
			case BuilderCatalog.Shape.Dome:
			case BuilderCatalog.Shape.QuarterDome:
				bool half = piece.Kind == BuilderCatalog.Shape.QuarterDome;
				Hollow( (u,v,inside) =>
				{
					float a = u * (half ? MathF.PI : MathF.Tau), b = v * MathF.PI/2;
					float r = x - (inside ? wall : 0);
					return new Vector3( MathF.Cos(a)*MathF.Cos(b)*r, MathF.Sin(a)*MathF.Cos(b)*r, MathF.Sin(b)*r - z );
				}, half ? 16 : 32, 8, !half ); break;
			case BuilderCatalog.Shape.Bend:
				float bendRadius = x, tubeRadius = x / 3;
				Hollow( (u,v,inside) =>
				{
					float a = u*MathF.Tau, b = v*piece.Angle*MathF.PI/180;
					float r = tubeRadius * (inside ? 0.75f : 1);
					return new Vector3( (bendRadius+r*MathF.Cos(a))*MathF.Cos(b), (bendRadius+r*MathF.Cos(a))*MathF.Sin(b), -r*MathF.Sin(a) );
				}, 16, Math.Max( 4, (int)piece.Angle/10 ), true ); break;
			default:
				bool open = piece.Kind == BuilderCatalog.Shape.HalfPipe;
				Hollow( (u,v,inside) =>
				{
					float a = u * (open ? MathF.PI : MathF.Tau);
					float r = inside ? x-wall : x;
					if ( piece.Kind == BuilderCatalog.Shape.PlateHole )
						r = inside ? x*0.65f : x / MathF.Max( MathF.Abs(MathF.Cos(a)), MathF.Abs(MathF.Sin(a)) );
					return new Vector3( MathF.Cos(a)*r, MathF.Sin(a)*r, -z+size.z*v );
				}, open ? 16 : 32, 1, !open ); break;
		}
	}
}
