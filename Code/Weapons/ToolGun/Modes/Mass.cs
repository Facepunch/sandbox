﻿
[Icon( "🍔" )]
[Title( "#tool.name.mass" )]
[ClassName( "mass" )]
[Group( "#tool.group.tools" )]
public class Mass : ToolMode
{
	[Sync, Property, Title( "Mass (kg)" ), Range( 1, 250 ), Step( 0.5f )]
	public float Value { get; set; } = 100.0f;

	public override string Description => "#tool.hint.mass.description";

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.mass.set", OnSetMass );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.mass.copy", OnCopyMass );
		RegisterAction( ToolInput.Reload, () => "#tool.hint.mass.reset", OnResetMass );
	}

	void OnSetMass()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var rb = select.GameObject.GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		SetMass( rb, Value );
		ShootEffects( select );
	}

	void OnCopyMass()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var rb = select.GameObject.GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		CopyMass( rb );
		ShootEffects( select );
	}

	void OnResetMass()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var rb = select.GameObject.GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		SetMass( rb, 0.0f );
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
