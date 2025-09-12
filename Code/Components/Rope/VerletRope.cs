namespace Sandbox;

/// <summary>
/// Verlet integration-based rope physics simulation component.
/// 
/// Key trade-offs:
/// - SegmentCount: Visual fidelity &amp; collision accuracy vs. performance (higher values lead to slower constraint solving and more physics traces)
/// - ConstraintIterations: Quality &amp; stiffness vs. performance (more iterations -> slower solving, but ropes are more rigid/less slack)
/// </summary>
public class VerletRope : Component
{
	/// <summary>
	/// The GameObject the end of the rope attaches to.
	/// </summary>
	[Property] public GameObject Attachment { get; set; }

	/// <summary>
	/// Will delete Attachment and our parent GameObject if either of us are deleted.
	/// </summary>
	[Property] public bool AutomaticCleanUp { get; set; }

	/// <summary>
	/// Number of segments in the rope. Higher values increase visual fidelity and collision accuracy but quickly reduce performance.
	/// </summary>
	[Property] public int SegmentCount { get; set; } = 20;

	/// <summary>
	/// Length of each rope segment in units.
	/// </summary>
	[Property] public float SegmentLength { get; set; } = 10.0f;

	/// <summary>
	/// Number of iterations to solve constraints. Higher values increase rigidity but reduce performance.
	/// </summary>
	[Property] public int ConstraintIterations { get; set; } = 100;

	/// <summary>
	/// Gravity vector applied to the rope.
	/// </summary>
	[Property] public Vector3 Gravity { get; set; } = new( 0, 0, -800 );

	/// <summary>
	/// Rope stiffness factor. Higher values make the rope more rigid.
	/// </summary>
	[Property] public float Stiffness { get; set; } = 0.7f;

	/// <summary>
	/// Dampens rope movement. Higher values make the rope settle faster.
	/// </summary>
	[Property] public float DampingFactor { get; set; } = 0.2f;

	/// <summary>
	/// Radius of the rope for collision detection.
	/// </summary>
	[Property] public float Radius { get; set; } = 1f;

	/// <summary>
	/// Controls how easily the rope bends. Lower values allow more bending, higher values make it stiffer.
	/// </summary>
	[Property] public float SoftBendFactor { get; set; } = 0.3f;

	/// <summary>
	/// Factor after which we consider a rope to be stretched.
	/// </summary>
	private float collisionMaxRopeStretchFactor { get; set; } = 1.1f;
	/// <summary>
	/// Ignore collisions when segment is stretched beyond this factor
	/// </summary>
	private float collisionMaxRopeSegmentStretchFactor { get; set; } = 1.7f;

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

	// Stretch detection
	private float currentRopeLength;
	private float averageSegmentLength;

	// Rest detection variables
	private Vector3 lastStartPos;
	private Vector3 lastEndPos;
	private TimeSince timeSinceRest;
	private bool isAtRest = false;
	private int currenRestFrameCount = 0;

	// Used for interpolation between physics updates.
	private TimeSince timeSinceSimulate;

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

		timeSinceSimulate = 0;
		timeSinceRest = 0;
	}

	protected override void OnUpdate()
	{
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
			var isAttached = (i == 0) || (i == SegmentCount - 1);
			points.Add( new RopePoint { Position = pos, Previous = pos, IsAttached = isAttached } );
		}
	}

	protected override void OnDestroy()
	{
		if ( AutomaticCleanUp && Attachment.IsValid() )
		{
			Attachment.Destroy();
		}

		base.OnDestroy();
	}

	public void Simulate( float dt )
	{
		if ( AutomaticCleanUp && !Attachment.IsValid() )
		{
			DestroyGameObject();
			return;
		}

		CheckAndWakeRope();

		if ( isAtRest ) return;

		ApplyForces();

		VerletIntegration( dt );

		UpdateRopeLengths();

		ApplyConstraints();

		HandleCollisions();

		CheckRestState();

		timeSinceSimulate = 0;
	}

	private void CheckAndWakeRope()
	{
		if ( isAtRest )
		{
			bool startMoved = (WorldPosition - lastStartPos).LengthSquared > 0.01f;
			bool endMoved = Attachment != null && (Attachment.WorldPosition - lastEndPos).LengthSquared > 0.01f;

			if ( startMoved || endMoved || timeSinceRest > 2f ) // Occasionally wake up ropes, so we can react to external collisions
			{
				isAtRest = false;
				currenRestFrameCount = 0;

				if ( timeSinceRest > 2f )
				{
					// only tick a single frame when waking up from a long rest
					currenRestFrameCount = restFramesRequired - 1;
				}
			}
		}

		// Update attachment positions for tracking
		lastStartPos = WorldPosition;
		lastEndPos = Attachment?.WorldPosition ?? lastEndPos;
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
		// Apply overall rope length constraint first
		// This drastically reduces the number of iterations we need
		// And only causes minimal artifacts
		// See https://toqoz.fyi/game-rope.html # Number of iterations
		ApplyOverallRopeConstraint();

		// Apply both stiffness and bending constraints in each iteration
		for ( var iteration = 0; iteration < ConstraintIterations; iteration++ )
		{
			for ( var i = 0; i < points.Count - 1; i++ )
			{
				// Stiffness constraints for adjacent points
				var p1 = points[i];
				var p2 = points[i + 1];

				var segment = p2.Position - p1.Position;
				var segmentLength = MathF.Sqrt( segment.LengthSquared );
				var stretch = segmentLength - SegmentLength;
				var direction = segment / segmentLength;
				var stretchStiffness = stretch * direction * Stiffness;

				if ( p1.IsAttached )
				{
					p2.Position -= stretchStiffness;
				}
				else if ( p2.IsAttached )
				{
					p1.Position += stretchStiffness;
				}
				else
				{
					p1.Position += stretchStiffness * 0.5f;
					p2.Position -= stretchStiffness * 0.5f;
				}

				points[i] = p1;
				points[i + 1] = p2;

				// Bending constraints for points two segments apart
				if ( i < points.Count - 2 )
				{
					var p3 = points[i + 2];

					var delta = p3.Position - p1.Position;
					var distSq = delta.LengthSquared;
					if ( distSq > 0.000001f )
					{
						var dist = MathF.Sqrt( distSq );
						var diff = (dist - SegmentLength * 2.0f) / dist;
						var offset = delta * SoftBendFactor * diff * SoftBendFactor;

						if ( !p1.IsAttached )
							p1.Position += offset;

						if ( !p3.IsAttached )
							p3.Position -= offset;

						points[i] = p1;
						points[i + 2] = p3;
					}
				}
			}
		}
	}

	void ApplyOverallRopeConstraint()
	{
		// Only apply if both ends are attached
		if ( points.Count < 2 || !points[0].IsAttached || !points[points.Count - 1].IsAttached )
			return;

		var first = points[0];
		var last = points[points.Count - 1];

		float currentDistance = (last.Position - first.Position).Length;

		// Maximum allowed length is the total rope length
		float maxLength = SegmentLength * (points.Count - 1);

		// Only constrain if the rope is stretched beyond its maximum length
		if ( currentDistance > maxLength )
		{
			var direction = (last.Position - first.Position).Normal;

			// Adjust the non-attached points along the rope
			for ( int i = 1; i < points.Count - 1; i++ )
			{
				if ( points[i].IsAttached )
					continue;

				float t = (float)i / (points.Count - 1);
				Vector3 idealPos = first.Position + direction * maxLength * t;

				var p = points[i];
				p.Position = Vector3.Lerp( p.Position, idealPos, 0.3f );
				points[i] = p;
			}
		}
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
			currenRestFrameCount++;
			if ( currenRestFrameCount >= restFramesRequired )
			{
				isAtRest = true;
				timeSinceRest = 0;
			}
		}
		else
		{
			currenRestFrameCount = 0;
		}
	}

	void Draw()
	{
		var line = GetComponent<LineRenderer>();
		if ( line is null ) return;

		if ( !Attachment.IsValid() )
		{
			line.Enabled = false;
			return;
		}

		// We could use InterpolationBuffer here but i feel like that would be overkill
		// Also it's private/internal.
		float fixedDelta = 1f / ProjectSettings.Physics.FixedUpdateFrequency.Clamp( 1, 1000 );
		float lerpFactor = Math.Min( timeSinceSimulate / fixedDelta, 1.0f );

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
		}
	}
}
