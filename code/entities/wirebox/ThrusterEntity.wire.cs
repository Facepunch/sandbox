using Sandbox;

public partial class ThrusterEntity : IWireInputEntity
{
	WirePortData IWireEntity.WirePorts { get; } = new WirePortData();
	public void WireInitialize()
	{
		this.RegisterInputHandler( "ForceMultiplier", ( float value ) =>
		{
			ForceMultiplier = value;
			Enabled = ForceMultiplier != 0.0f;
		} );
	}
}
