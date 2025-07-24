
[Icon( "🍔" )]
[ClassName( "mass" )]
public class Mass : ToolMode
{
	[Sync, Property, Title( "Mass (kg)" ), Range( 1, 1000 )]
	public float Value { get; set; } = 100.0f;

	public override void OnControl()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var rb = select.GameObject.GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		if ( Input.Pressed( "attack1" ) ) SetMass( rb, Value );
		else if ( Input.Pressed( "attack2" ) ) CopyMass( rb );
		else if ( Input.Pressed( "reload" ) ) SetMass( rb, 0.0f );
		else return;

		ShootEffects( select );
	}

	[Rpc.Host]
	private void SetMass( Rigidbody rb, float mass )
	{
		if ( rb.IsValid() && !rb.IsProxy ) rb.MassOverride = mass;
	}

	[Rpc.Host]
	private void CopyMass( Rigidbody rb )
	{
		if ( rb.IsValid() && !rb.IsProxy ) Value = rb.Mass;
	}
}
