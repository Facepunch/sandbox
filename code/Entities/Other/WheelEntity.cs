using Sandbox;

[Library( "ent_wheel" )]
public partial class WheelEntity : Prop
{
	public HingeJoint Joint;

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( Joint.IsValid() )
		{
			Joint.Remove();
		}
	}

	protected override void UpdatePropData( Model model )
	{
		base.UpdatePropData( model );

		Health = -1;
	}
}
