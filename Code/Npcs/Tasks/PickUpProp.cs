namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Tells the AnimationLayer to pick up and hold a prop.
/// </summary>
public class PickUpProp : TaskBase
{
	public GameObject Target { get; set; }

	public PickUpProp( GameObject target )
	{
		Target = target;
	}

	protected override void OnStart()
	{
		Npc.Animation.SetHeldProp( Target );
	}

	protected override TaskStatus OnUpdate()
	{
		return TaskStatus.Success;
	}
}
