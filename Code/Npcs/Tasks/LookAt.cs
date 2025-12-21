namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Tells the AnimationLayer to face a target position or object
/// </summary>
public class LookAt : TaskBase
{
	public Vector3? TargetPosition { get; set; }
	public GameObject TargetObject { get; set; }

	public LookAt( Vector3 targetPosition )
	{
		TargetPosition = targetPosition;
	}

	public LookAt( GameObject gameObject )
	{
		TargetObject = gameObject;
	}

	protected override TaskStatus OnUpdate()
	{
		var targetPos = GetTargetPosition();
		if ( !targetPos.HasValue )
			return TaskStatus.Failed;

		Npc.Animation.LookAt( targetPos.Value );

		return Npc.Animation.IsFacingTarget() ? TaskStatus.Success : TaskStatus.Running;
	}

	private Vector3? GetTargetPosition()
	{
		if ( TargetObject.IsValid() ) return TargetObject.WorldPosition;
		return TargetPosition;
	}
}
