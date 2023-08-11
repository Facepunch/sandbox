using Sandbox;
public partial class LampEntity : IWireInputEntity
{
	WirePortData IWireEntity.WirePorts { get; } = new WirePortData();
	public void WireInitialize()
	{
		this.RegisterInputHandler( "On", ( bool value ) =>
		{
			Enabled = value;
		} );
	}
}
