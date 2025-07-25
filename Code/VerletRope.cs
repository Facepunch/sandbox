public class VerletRope : Component
{
	[Property] public GameObject Attachment { get; set; }
	[Property] public int SegmentCount { get; set; } = 20;
	[Property] public float SegmentLength { get; set; } = 10.0f;
	[Property] public int ConstraintIterations { get; set; } = 2;
	[Property] public Vector3 Gravity { get; set; } = new( 0, 0, -800 );
	[Property] public float Stiffness { get; set; } = 0.7f;
	[Property] public float DampingFactor { get; set; } = 0.2f;
	[Property] public float Width { get; set; } = 1f;
	[Property] public int SimulationFrequency { get; set; } = 60;

	/// <summary>
	/// Factor after which we consider a rope to be stretched.
	/// </summary>
	private float collisionMaxRopeStretchFactor { get; set; } = 2f;
	/// <summary>
	/// Ignore collisions when segment is stretched beyond this factor
	/// </summary>
	private float collisionMaxRopeSegmentStretchFactor { get; set; } = 1.6f;

	/// <summary>
	/// Velocity threshold below which we consider the rope to be at rest.
	/// </summary>
	private float restVelocityThreshold { get; set; } = 0.03f;
	/// <summary>
	/// Consecutive frames of not movement required to consider the rope at rest.
	/// </summary>
	private int restFramesRequired { get; set; } = 8;

	private float currentRopeLength;
	private float averageSegmentLength;
	private bool isAtRest = false;
	private int restFrameCount = 0;
	private Vector3 lastStartPos;
	private Vector3 lastEndPos;

	private struct RopePoint
	{
		public Vector3 Position;
		public Vector3 Previous;
		public Vector3 Acceleration;
		public bool IsAttached;
		public float MovementSinceLastCollision;
	}

	private List<RopePoint> points;

	protected override void OnEnabled()
	{
		InitializePoints();
		for ( int i = 0; i < 10; i++ )
		{
			Simulate( 1.0f / 60.0f );
		}

		// Initialize attachment tracking
		lastStartPos = WorldPosition;
		lastEndPos = Attachment?.WorldPosition ?? (WorldPosition + Vector3.Down * SegmentLength * SegmentCount);
	}

	protected override void OnFixedUpdate()
	{
		// Check if we need to wake up the rope due to attachment movement
		if ( isAtRest )
		{
			bool startMoved = (WorldPosition - lastStartPos).LengthSquared > 0.01f;
			bool endMoved = Attachment != null && (Attachment.WorldPosition - lastEndPos).LengthSquared > 0.01f;

			if ( startMoved || endMoved )
			{
				WakeRope();
			}
			else
			{
				return;
			}
		}

		float fixedTimeStep = 1.0f / (float)SimulationFrequency;

		// Calculate how many substeps we need
		int substeps = Math.Max( 1, MathX.CeilToInt( Time.Delta / fixedTimeStep ) );
		float substepDelta = Time.Delta / substeps;

		// Run simulation substeps
		for ( int i = 0; i < substeps; i++ )
		{
			Simulate( substepDelta );
		}

		// Update attachment positions for tracking
		lastStartPos = WorldPosition;
		lastEndPos = Attachment?.WorldPosition ?? lastEndPos;
	}

	protected override void OnUpdate()
	{
		Draw();
	}

	private void WakeRope()
	{
		isAtRest = false;
		restFrameCount = 0;
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
			var isAttached = (i == 0) || (i == SegmentCount - 1);
			points.Add( new RopePoint { Position = pos, Previous = pos, IsAttached = isAttached } );
		}
	}

	void Simulate( float dt )
	{
		ApplyForces();
		VerletIntegration( dt );
		ApplyConstraints();

		UpdateRopeLengths();

		HandleCollisions();

		CheckRestState();
	}

	private void CheckRestState()
	{
		if ( isAtRest )
			return;

		bool isMoving = false;
		float velocityThresholdSq = restVelocityThreshold * restVelocityThreshold;

		// Check if any non-attached point is moving significantly
		for ( int i = 0; i < points.Count; i++ )
		{
			var p = points[i];

			// Skip attached points as they're controlled externally
			if ( p.IsAttached )
				continue;

			var velocitySq = (p.Position - p.Previous).LengthSquared;

			if ( velocitySq > velocityThresholdSq )
			{
				isMoving = true;
				break;
			}
		}

		if ( !isMoving )
		{
			restFrameCount++;
			if ( restFrameCount >= restFramesRequired )
			{
				isAtRest = true;
			}
		}
		else
		{
			restFrameCount = 0;
		}
	}

	void VerletIntegration( float dt )
	{
		for ( int i = 0; i < points.Count; i++ )
		{
			var p = points[i];

			if ( p.IsAttached )
			{
				// Update attached points position
				if ( i == 0 )
					p.Position = WorldPosition;
				else if ( i == points.Count - 1 && Attachment != null )
					p.Position = Attachment.WorldPosition;

				points[i] = p;
				continue;
			}

			Vector3 velocity = p.Position - p.Previous;

			var currentPosition = p.Position;

			p.Position = currentPosition + velocity * (1.0f - DampingFactor * dt) + p.Acceleration * (dt * dt);
			p.Previous = currentPosition;

			points[i] = p;
		}
	}

	void ApplyForces()
	{
		for ( int i = 0; i < points.Count; i++ )
		{
			var p = points[i];

			if ( p.IsAttached )
				continue;

			var totalAcceleration = Gravity;

			// Apply damping
			var velocity = p.Position - p.Previous;
			var drag = -DampingFactor * velocity.Length * velocity;
			totalAcceleration += drag;

			p.Acceleration = totalAcceleration;
			points[i] = p;
		}
	}

	void ApplyConstraints()
	{
		// Apply stiffness constraints
		for ( var iteration = 0; iteration < ConstraintIterations; iteration++ )
		{
			for ( var i = 0; i < points.Count - 1; i++ )
			{
				var p1 = points[i];
				var p2 = points[i + 1];

				var segment = p2.Position - p1.Position;
				var stretch = segment.Length - SegmentLength;
				var direction = segment.Normal;

				if ( p1.IsAttached )
				{
					p2.Position -= direction * stretch * Stiffness;
				}
				else if ( p2.IsAttached )
				{
					p1.Position += direction * stretch * Stiffness;
				}
				else
				{
					p1.Position += direction * stretch * 0.5f * Stiffness;
					p2.Position -= direction * stretch * 0.5f * Stiffness;
				}

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

				if ( !p1.IsAttached )
					p1.Position += offset;

				if ( !p3.IsAttached )
					p3.Position -= offset;

				points[i] = p1;
				points[i + 2] = p3;
			}
		}
	}


	private void UpdateRopeLengths()
	{
		float totalLength = 0f;
		int segments = 0;

		for ( int i = 0; i < points.Count - 1; i++ )
		{
			float segmentLength = (points[i + 1].Position - points[i].Position).Length;
			totalLength += segmentLength;
			segments++;
		}

		currentRopeLength = totalLength;
		averageSegmentLength = segments > 0 ? totalLength / segments : SegmentLength;
	}

	/// <summary>
	/// This method checks each segment of the rope for collisions and adjusts their positions accordingly.
	/// It skips collision checks for segments that are excessively stretched to prevent the rope from becoming unstable.
	/// If the rope is extremely stretched, all collision checks are bypassed to allow the rope to recover.
	/// </summary>
	void HandleCollisions()
	{
		var segmentSlideIgnoreLength = averageSegmentLength * collisionMaxRopeSegmentStretchFactor;
		var isRopeStretched = currentRopeLength > SegmentLength * SegmentCount * collisionMaxRopeStretchFactor;

		// Last resort disable all collisions briefly in an attempt to recover the rope
		var isExtremelyStretched = currentRopeLength > SegmentLength * SegmentCount * 4;
		if ( isExtremelyStretched )
		{
			return;
		}

		for ( int i = 1; i < points.Count; i++ )
		{
			if ( points[i].IsAttached ) continue;

			var p = points[i];

			p.MovementSinceLastCollision += (p.Position - p.Previous).LengthSquared;

			if ( p.MovementSinceLastCollision < 0.01f * 0.01f )
			{
				// Skip if movement is too small
				points[i] = p;
				continue;
			}

			// Skip collision check for stretched segments
			// This is our attempt to unfuck the rope if it got dragged across the map
			if ( isRopeStretched )
			{
				var prevPoint = points[i - 1];
				var currentSegmentLengthSquared = (prevPoint.Position - p.Position).LengthSquared;

				if ( currentSegmentLengthSquared > segmentSlideIgnoreLength * segmentSlideIgnoreLength )
				{
					points[i] = p;
					continue;
				}
			}

			p.MovementSinceLastCollision = 0.0f; // Reset movement after processing

			// First check for movement-based collisions (from previous to current position)
			var moveTrace = Scene.Trace.Sphere( Width, p.Previous, p.Position )
				.UseHitPosition( true )
				.Run();

			if ( moveTrace.Hit )
			{
				// Prevent the rope from clipping through the ground
				// Would be nice if we could check for backface collision
				// but that only seems to be available for UseRenderMesh traces.
				if ( moveTrace.Normal.z < -0.5f )
				{
					p.Position = moveTrace.HitPosition + Vector3.Up;
				}
				else
				{
					// Hit something during movement
					p.Position = moveTrace.EndPosition + moveTrace.Normal * 0.01f;
				}

				// Project velocity along surface for sliding
				if ( isRopeStretched )
				{
					var velocity = p.Position - p.Previous;
					var slideVelocity = velocity - Vector3.Dot( velocity, moveTrace.Normal ) * moveTrace.Normal;
					p.Previous = p.Position - slideVelocity * (1.0f - DampingFactor);
				}
			}

			points[i] = p;
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
