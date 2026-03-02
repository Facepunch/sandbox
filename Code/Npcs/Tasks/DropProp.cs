namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Tells the AnimationLayer to drop the held prop.
/// </summary>
public class DropProp : TaskBase
{
	public GameObject Target { get; set; }

	public DropProp( GameObject target )
	{
		Target = target;
	}

	protected override void OnStart()
	{
		Npc.Animation.ClearHeldProp();
	}

	protected override TaskStatus OnUpdate()
	{
		return TaskStatus.Success;
	}
}
