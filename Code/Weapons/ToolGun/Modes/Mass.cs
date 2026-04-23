﻿
[Icon( "🍔" )]
[ClassName( "mass" )]
[Group( "Tools" )]
public class Mass : ToolMode
{
	[Sync, Property, Title( "Mass (kg)" ), Range( 1, 250 ), Step( 0.5f )]
	public float Value { get; set; } = 100.0f;

	public override string Description => "#tool.hint.mass.description";
	public override string PrimaryAction => "#tool.hint.mass.set";
	public override string SecondaryAction => "#tool.hint.mass.copy";
	public override string ReloadAction => "#tool.hint.mass.reset";

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
		if ( !rb.IsValid() || rb.IsProxy ) return;

		if ( mass <= 0f )
		{
			rb.GetComponent<PhysicalProperties>()?.Destroy();
			rb.MassOverride = 0f;
			return;
		}

		var mo = rb.GetOrAddComponent<PhysicalProperties>();
		mo.Mass = mass;
		mo.Apply();
	}

	[Rpc.Host]
	private void CopyMass( Rigidbody rb )
	{
		if ( !rb.IsValid() || rb.IsProxy ) return;

		var mo = rb.GetComponent<PhysicalProperties>();
		Value = mo.IsValid() ? mo.Mass : rb.Mass;
	}
}
