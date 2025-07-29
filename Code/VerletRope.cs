public class VerletRope : Component, IScenePhysicsEvents
{
	[Property] public GameObject Attachment { get; set; }
	[Property] public int SegmentCount { get; set; } = 20;
	[Property] public float SegmentLength { get; set; } = 10.0f;
	[Property] public int ConstraintIterations { get; set; } = 100;
	[Property] public Vector3 Gravity { get; set; } = new( 0, 0, -800 );
	[Property] public float Stiffness { get; set; } = 0.7f;
	[Property] public float DampingFactor { get; set; } = 0.2f;
	[Property] public float Radius { get; set; } = 1f;
	// Lower values make the rope bend more easily, higher values make it stiffer
	[Property] public float SoftBendFactor { get; set; } = 0.3f;

	/// <summary>
	/// Factor after which we consider a rope to be stretched.
	/// </summary>
	private float collisionMaxRopeStretchFactor { get; set; } = 1.1f;
	/// <summary>
	/// Ignore collisions when segment is stretched beyond this factor
	/// </summary>
	private float collisionMaxRopeSegmentStretchFactor { get; set; } = 1.2f;

	/// <summary>
	/// Velocity threshold below which we consider the rope to be at rest.
	/// </summary>
	private float restVelocityThreshold => baseRestVelocityThreshold * (SegmentLength / baseSegmentLength); // Scale the rest velocity threshold based on segment length

	private float slidingVelocityThreshold => restVelocityThreshold * 5f;

	/// <summary>
	/// Base velocity threshold used for scaling the rest detection
	/// </summary>
	private static readonly float baseRestVelocityThreshold = 0.03f;

	/// <summary>
	/// Base segment length used for calibrating various calculations.
	/// </summary>
	private static readonly float baseSegmentLength = 16f;

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
	private TimeSince timeSincePhysicsUpdate;

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

		timeSincePhysicsUpdate = 0;
	}

	void IScenePhysicsEvents.PrePhysicsStep()
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

		Simulate( Time.Delta );

		timeSincePhysicsUpdate = 0;

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

		UpdateRopeLengths();

		ApplyConstraints();

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
		for ( var iteration = 0; iteration < ConstraintIterations; iteration++ )
		{
			// Doing both a forward and backwards pass increases conversion speed.
			for ( var i = 0; i < points.Count - 1; i++ )
			{
				ApplyConstraintBetweenPoints( i, i + 1 );

			}

			for ( var i = points.Count - 2; i >= 0; i-- )
			{
				ApplyConstraintBetweenPoints( i, i + 1 );
			}

			// Apply bending constraints at the end
			for ( var i = 0; i < points.Count - 2; i++ )
			{
				ApplyBendingConstraint( i, i + 2 );
			}
		}
	}

	private void ApplyConstraintBetweenPoints( int indexA, int indexB )
	{
		var p1 = points[indexA];
		var p2 = points[indexB];

		var segment = p2.Position - p1.Position;
		var segmentLength = MathF.Sqrt( segment.LengthSquared );

		if ( segmentLength < 0.001f )
			return; // Avoid division by zero

		var stretch = segmentLength - SegmentLength;
		var direction = segment / segmentLength;

		// Calculate a stiffness modifier based on attachment points
		float stiffnessModifier = Stiffness;

		// Points near attachments get stronger correction
		if ( p1.IsAttached )
		{
			p2.Position -= direction * stretch * stiffnessModifier;
		}
		else if ( p2.IsAttached )
		{
			p1.Position += direction * stretch * stiffnessModifier;
		}
		else
		{
			// For middle points, we apply a balanced correction
			p1.Position += direction * stretch * 0.5f * stiffnessModifier;
			p2.Position -= direction * stretch * 0.5f * stiffnessModifier;
		}

		points[indexA] = p1;
		points[indexB] = p2;
	}

	private void ApplyBendingConstraint( int indexA, int indexC )
	{
		var p1 = points[indexA];
		var p3 = points[indexC];

		var delta = p3.Position - p1.Position;
		var distSq = delta.LengthSquared;

		if ( distSq < 0.000001f )
			return;

		var dist = MathF.Sqrt( distSq );
		var targetLength = SegmentLength * MathF.Abs( indexC - indexA );
		var diff = (dist - targetLength) / dist;

		// Softer bend constraint (0.5 factor)
		var offset = delta * SoftBendFactor * diff * SoftBendFactor;

		if ( !p1.IsAttached )
			p1.Position += offset;

		if ( !p3.IsAttached )
			p3.Position -= offset;

		points[indexA] = p1;
		points[indexC] = p3;
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

			var plannedMovementDistanceSquared = (p.Position - p.Previous).LengthSquared;
			p.MovementSinceLastCollision += plannedMovementDistanceSquared;


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
				if ( plannedMovementDistanceSquared > segmentSlideIgnoreLength * segmentSlideIgnoreLength )
				{
					points[i] = p;

					continue;
				}
			}

			p.MovementSinceLastCollision = 0.0f; // Reset movement after processing

			// First check for movement-based collisions (from previous to current position)
			var moveTrace = Scene.Trace.Sphere( Radius, p.Previous, p.Position )
				.UseHitPosition( true )
				.Run();

			if ( moveTrace.Hit )
			{
				var originalMove = p.Position - p.Previous;

				Vector3 newPosition;
				// Determine base collision response position
				if ( moveTrace.Normal.z < -0.5f )
				{
					// Prevent clipping through ground
					newPosition = moveTrace.HitPosition + Vector3.Up;
				}
				else
				{
					// Hit something during movement
					newPosition = moveTrace.EndPosition + moveTrace.Normal * 0.01f;
				}

				// Apply sliding behavior with surface friction

				// Calculate sliding component (project movement onto surface plane)
				float dot = Vector3.Dot( originalMove, moveTrace.Normal );
				Vector3 normalComponent = moveTrace.Normal * dot;
				Vector3 slideComponent = originalMove - normalComponent;

				// Apply surface friction to the slide
				float frictionFactor = Math.Clamp( moveTrace.Surface.Friction, 0.1f, 0.95f );
				slideComponent *= (1.0f - frictionFactor);

				// Dont apply slide if it's too small
				// so rope comes to rest faster
				if ( slideComponent.LengthSquared > slidingVelocityThreshold * slidingVelocityThreshold )
				{
					// Add the dampened slide to our position
					newPosition += slideComponent;
				}


				p.Position = newPosition;
			}

			points[i] = p;
		}
	}

	void Draw()
	{
		var line = GetComponent<LineRenderer>();
		if ( line is null ) return;

		// We could use InterpolationBuffer here but i feel like that would be overkill
		// Also it's private/internal.
		float fixedDelta = 1f / ProjectSettings.Physics.FixedUpdateFrequency.Clamp( 1, 1000 );
		float lerpFactor = Math.Min( timeSincePhysicsUpdate / fixedDelta, 1.0f );

		line.UseVectorPoints = true;
		line.VectorPoints ??= new();
		line.VectorPoints.Clear();

		for ( int i = 0; i < points.Count; i++ )
		{
			var point = points[i];

			// For attached points, always use their current position
			if ( point.IsAttached )
			{
				if ( i == 0 )
					line.VectorPoints.Add( WorldPosition );
				else if ( i == points.Count - 1 && Attachment != null )
					line.VectorPoints.Add( Attachment.WorldPosition );
				else
					line.VectorPoints.Add( point.Position );
			}
			else
			{
				// For non-attached points, lerp between previous and current position
				Vector3 lerpedPosition = Vector3.Lerp( point.Previous, point.Position, lerpFactor );
				line.VectorPoints.Add( lerpedPosition );
			}

			//DebugOverlay.Sphere( new Sphere( point.Position, Radius * 2f ), Color.Red );
		}
	}
}
