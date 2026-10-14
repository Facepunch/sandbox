namespace Sandbox.Npcs;

public partial class Npc
{
	public bool IsGrounded => GroundObject.IsValid();

	/// <summary>
	/// The object we're standing on. Null if we're standing on nothing.
	/// </summary>
	public GameObject GroundObject { get; set; }

	/// <summary>
	/// The collider component we're standing on. Null if we're standing nothing
	/// </summary>
	public Component GroundComponent { get; set; }

	/// <summary>
	/// If we're stnding on a surface this is it
	/// </summary>
	public Surface GroundSurface { get; set; }

	/// <summary>
	/// Amount of time since this character was last on the ground
	/// </summary>
	public TimeSince TimeSinceGrounded { get; private set; } = 0;

	/// <summary>
	/// Amount of time since this character was last not on the ground
	/// </summary>
	public TimeSince TimeSinceUngrounded { get; private set; } = 0;

	void CategorizeGround()
	{
		if ( !_navAgent.IsValid() )
			return;

		var up = WorldRotation.Up;
		var from = WorldPosition + up * 4;
		var to = WorldPosition - up * 2;

		float radiusScale = 1;
		var tr = TraceBody( from, to, radiusScale, 0.5f );

		while ( tr.StartedSolid )
		{
			radiusScale = radiusScale - 0.1f;
			if ( radiusScale < 0.7f )
			{
				UpdateGroundFromTraceResult( default );
				return;
			}

			tr = TraceBody( from, to, radiusScale, 0.5f );
		}

		if ( tr.StartedSolid )
		{
			UpdateGroundFromTraceResult( default );
			return;
		}

		if ( !tr.StartedSolid && tr.Hit )
		{
			UpdateGroundFromTraceResult( tr );
		}
		else
		{
			UpdateGroundFromTraceResult( default );
		}
	}

	void UpdateGroundFromTraceResult( SceneTraceResult tr )
	{
		bool wasGrounded = IsGrounded;

		var body = tr.Body;
		GroundObject = body?.GameObject;
		GroundComponent = body?.Component;
		GroundSurface = tr.Surface;

		if ( IsGrounded != wasGrounded )
		{
			OnGroundedChanged();
		}
	}

	private void OnGroundedChanged()
	{
		_rigidbody.MotionEnabled = !IsGrounded;
		_navAgent.Enabled = IsGrounded;
	}

	/// <summary>
	/// Return an aabb representing the body
	/// </summary>
	public BBox BodyBox( float scale = 1.0f, float heightScale = 1.0f )
	{
		var radius = _navAgent.Radius * 0.5f * scale;
		var height = _navAgent.Height * heightScale;
		var up = WorldRotation.Up;
		var center = up * height * 0.5f;
		var extents = new Vector3( radius ) + up.Abs() * (height * 0.5f - radius);

		return new BBox( center - extents, center + extents );
	}

	/// <summary>
	/// Trace the aabb body from one position to another and return the result
	/// </summary>
	public SceneTraceResult TraceBody( Vector3 from, Vector3 to, float scale = 1.0f, float heightScale = 1.0f )
	{
		return Scene.Trace.Box( BodyBox( scale, heightScale ), from, to )
								.IgnoreGameObjectHierarchy( GameObject )
								.WithCollisionRules( Tags )
								.Run();
	}
}
