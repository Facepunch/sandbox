/// <summary>
/// A non-physics logical link between two GameObjects.
/// Used by the Linker tool to group unconnected objects so the Duplicator
/// treats them as part of the same contraption.
/// </summary>
public sealed class ManualLink : Component
{
	[Property, Sync]
	public GameObject Body { get; set; }

	protected override void OnDestroy()
	{
		if ( Body.IsValid() )
			Body.Destroy();

		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if ( !Body.IsValid() )
			DestroyGameObject();
	}
}

