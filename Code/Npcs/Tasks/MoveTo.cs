namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that commands the NavigationLayer to move to a target
/// </summary>
public class MoveTo : TaskBase
{
	public Vector3 TargetPosition { get; set; }
	public float StopDistance { get; set; } = 10f;

	public MoveTo( Vector3 targetPosition, float stopDistance = 10f )
	{
		TargetPosition = targetPosition;
		StopDistance = stopDistance;
	}

	protected override void OnStart()
	{
		Npc.Navigation.MoveTo( TargetPosition, StopDistance );
	}

	protected override TaskStatus OnUpdate()
	{
		// Check if we've reached the target
		return Npc.Navigation.HasReachedTarget() ? TaskStatus.Success : TaskStatus.Running;
	}
}
