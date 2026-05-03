namespace Sandbox.Npcs.Layers;

/// <summary>
/// Handles Npc navigation
/// </summary>
public class NavigationLayer : BaseNpcLayer
{
	public NavMeshAgent Agent { get; private set; }

	public Vector3? MoveTarget { get; private set; }

	[Property]
	public float StopDistance { get; private set; } = 10f;

	/// <summary>
	/// The desired movement speed for the agent. Schedules can raise this to make the NPC run.
	/// </summary>
	public float WishSpeed { get; set; } = 100f;

	// Grace period after issuing a move before we allow failure checks,
	// so the agent has time to start navigating.
	private TimeSince _timeSinceLastMoveIssued;

	protected override void OnStart()
	{
		Agent = Npc.GetComponent<NavMeshAgent>();

		// We handle rotation ourselves (facing look target or movement direction),
		// so prevent the agent from snapping the body to the path direction.
		if ( Agent.IsValid() )
			Agent.UpdateRotation = false;
	}

	/// <summary>
	/// Command this layer to move to a target
	/// </summary>
	public void MoveTo( Vector3 target, float stopDistance = 10f )
	{
		MoveTarget = target;
		StopDistance = stopDistance;
		_timeSinceLastMoveIssued = 0;

		if ( Agent.IsValid() )
		{
			Agent.MoveTo( target );

			// Use the agent's resolved navmesh position so distance checks are accurate
			if ( Agent.TargetPosition.HasValue )
			{
				MoveTarget = Agent.TargetPosition.Value;
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( Agent.IsValid() )
		{
			Agent.MaxSpeed = WishSpeed;
			// Use the NPC's actual body rotation (not the agent's path rotation) as the
			// reference frame so that animation blend parameters (move_x / move_y) are
			// computed relative to where the body is actually facing.
			Npc.Animation.SetMove( Agent.Velocity, Npc.WorldRotation );

			// If we have a pending move target but the agent isn't navigating,
			// and the navmesh just finished building, re-issue the MoveTo so the
			// agent can register on the freshly-built surface.
			if ( MoveTarget.HasValue && !Agent.IsNavigating
				&& !Npc.Scene.NavMesh.IsGenerating && !Npc.Scene.NavMesh.IsDirty
				&& _timeSinceLastMoveIssued > 0.1f )
			{
				Agent.MoveTo( MoveTarget.Value );
				_timeSinceLastMoveIssued = 0;
			}
		}
	}

	public override string GetDebugString()
	{
		if ( !MoveTarget.HasValue ) return null;

		var status = GetStatus();
		var dist = Npc.WorldPosition.Distance( MoveTarget.Value ).CeilToInt();
		return $"Nav: {status} ({dist}u)";
	}

	/// <summary>
	/// Current navigation status — reached target, still moving, or failed.
	/// </summary>
	public TaskStatus GetStatus()
	{
		if ( !MoveTarget.HasValue ) return TaskStatus.Success;

		var distance = Npc.WorldPosition.Distance( MoveTarget.Value );

		// Npc.DebugOverlay.Sphere( new Sphere( MoveTarget.Value, 16 ), Color.Green, 0.1f );

		if ( distance <= StopDistance )
			return TaskStatus.Success;

		// If the navmesh is still building (e.g. after procedural geometry was spawned),
		// keep waiting rather than failing immediately.
		if ( Npc.Scene.NavMesh.IsGenerating || Npc.Scene.NavMesh.IsDirty )
			return TaskStatus.Running;

		// Give the agent a short grace period to start navigating after a move is issued.
		if ( _timeSinceLastMoveIssued < 0.1f )
			return TaskStatus.Running;

		if ( Agent.IsValid() && !Agent.IsNavigating )
			return TaskStatus.Failed;

		return TaskStatus.Running;
	}

	public override void Reset()
	{
		MoveTarget = null;

		if ( Agent.IsValid() )
		{
			Agent.Stop();
		}
	}
}
