/// <summary>
/// Applies a mass override to the root Rigidbody of this object's hierarchy.
/// Attach this to any GameObject to persist mass across duplication.
/// </summary>
public sealed class MassOverride : Component
{
	[Property, Sync]
	public float Mass { get; set; } = 100f;

	protected override void OnStart() => Apply();
	protected override void OnEnabled() => Apply();

	public void Apply()
	{
		var rb = GameObject.Root.GetComponent<Rigidbody>();
		if ( rb.IsValid() ) rb.MassOverride = Mass;
	}
}
