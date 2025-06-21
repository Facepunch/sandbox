public class VerletRope : Component
{
	[Property] public GameObject Attachment { get; set; }
	[Property] public int SegmentCount { get; set; } = 20;
	[Property] public float SegmentLength { get; set; } = 10.0f;
	[Property] public int ConstraintIterations { get; set; } = 5;
	[Property] public Vector3 Gravity { get; set; } = new( 0, 0, -800 );

	private struct RopePoint
	{
		public Vector3 Position;
		public Vector3 Previous;
	}

	private List<RopePoint> points;

	protected override void OnEnabled()
	{
		InitializePoints();
		for ( int i = 0; i < 10; i++ )
		{
			Simulate( 1.0f / 60.0f );
			SatisfyConstraints();
		}
	}

	protected override void OnUpdate()
	{
		Simulate( Time.Delta );
		SatisfyConstraints();
		Draw();
	}

	void InitializePoints()
	{
		points = new();

		var start = WorldPosition;
		var direction = (Attachment?.WorldPosition ?? (start + Vector3.Down)) - start;
		direction = direction.Normal;

		for ( int i = 0; i < SegmentCount; i++ )
		{
			var pos = start + direction * SegmentLength * i;
			points.Add( new RopePoint { Position = pos, Previous = pos } );
		}
	}

	void Simulate( float dt )
	{
		float damping = MathF.Pow( 0.99f, dt * 60.0f );

		for ( int i = 1; i < points.Count - 1; i++ )
		{
			var p = points[i];
			var velocity = (p.Position - p.Previous) * damping;

			p.Previous = p.Position;
			p.Position += velocity + Gravity * dt * dt;

			var tr = Scene.Trace
				.Ray( p.Previous, p.Position )
				.Radius( 1.0f )
				.Run();

			if ( tr.Hit )
			{
				p.Position = tr.EndPosition + tr.Normal * 0.1f;

				// Kill velocity into the surface
				Vector3 vel = p.Position - p.Previous;
				Vector3 intoNormal = Vector3.Dot( vel, tr.Normal ) * tr.Normal;

				p.Previous = p.Position - (vel - intoNormal); // remove bounce component
			}

			points[i] = p;
		}

		points[0] = points[0] with { Position = WorldPosition };

		if ( Attachment != null )
			points[^1] = points[^1] with { Position = Attachment.WorldPosition };
	}

	void SatisfyConstraints()
	{
		for ( int iter = 0; iter < ConstraintIterations; iter++ )
		{
			for ( int i = 0; i < points.Count - 1; i++ )
			{
				var p1 = points[i];
				var p2 = points[i + 1];

				var delta = p2.Position - p1.Position;
				var dist = delta.Length;
				if ( dist <= 0.001f ) continue;

				var diff = (dist - SegmentLength) / dist;
				var offset = delta * 0.5f * diff;

				if ( i != 0 )
					p1.Position += offset;

				if ( i + 1 != points.Count - 1 )
					p2.Position -= offset;

				points[i] = p1;
				points[i + 1] = p2;
			}

			// Bending constraints
			for ( int i = 0; i < points.Count - 2; i++ )
			{
				var p1 = points[i];
				var p3 = points[i + 2];

				var delta = p3.Position - p1.Position;
				var dist = delta.Length;
				if ( dist <= 0.001f ) continue;

				var targetDist = SegmentLength * 2.0f;
				var diff = (dist - targetDist) / dist;
				var offset = delta * 0.5f * diff * 0.5f; // 0.5 = soft bend

				if ( i != 0 )
					p1.Position += offset;

				if ( i + 2 != points.Count - 1 )
					p3.Position -= offset;

				points[i] = p1;
				points[i + 2] = p3;
			}
		}
	}

	void Draw()
	{
		var line = GetComponent<LineRenderer>();
		if ( line is null ) return;

		line.UseVectorPoints = true;
		line.VectorPoints ??= new();
		line.VectorPoints.Clear();
		line.VectorPoints.AddRange( points.Select( p => p.Position ) );
	}
}
