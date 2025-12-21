namespace Sandbox.Npcs.Layers;

/// <summary>
/// Handles Npc navigation
/// </summary>
public class NavigationLayer : BaseNpcLayer
{
	public NavMeshAgent Agent { get; private set; }

	public Vector3? MoveTarget { get; private set; }
	public float StopDistance { get; private set; } = 10f;

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
		}
	}

	protected override void OnUpdate()
	{
		if ( Agent.IsValid() )
		{
			Npc.Animation.SetMove( Agent.Velocity, Agent.WorldRotation );
		}
	}

	/// <summary>
	/// Check if we've reached our target
	/// </summary>
	public bool HasReachedTarget()
	{
		if ( !MoveTarget.HasValue ) return true;

		var distance = Npc.WorldPosition.Distance( MoveTarget.Value );
		return distance <= StopDistance;
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
