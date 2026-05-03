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

	protected override void OnStart()
	{
		Agent = Npc.GetComponent<NavMeshAgent>();
	}

	/// <summary>
	/// Command this layer to move to a target
	/// </summary>
	public void MoveTo( Vector3 target, float stopDistance = 10f )
	{
		MoveTarget = target;
		StopDistance = stopDistance;

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
			Npc.Animation.SetMove( Agent.Velocity, Agent.WorldRotation );
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

		if ( Agent.IsValid() && !Agent.IsNavigating )
			return TaskStatus.Failed;

		return TaskStatus.Running;
	}

	public override void ResetLayer()
	{
		MoveTarget = null;

		if ( Agent.IsValid() )
		{
			Agent.Stop();
		}
	}
}
