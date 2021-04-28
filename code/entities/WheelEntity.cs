using Sandbox;
using Sandbox.Joints;

[Library( "ent_wheel" )]
public partial class WheelEntity : Prop, IPhysicsUpdate, IUse
{
	public enum WheelDirection
	{
		None,
		Clockwise,
		CounterClockwise
	}

	public RevoluteJoint Joint;
	private Vector3 torque = new Vector3( 0, 300000, 0 );
	private WheelDirection direction = WheelDirection.None;

	public void OnPostPhysicsStep( float delta )
	{
		if ( IsClient )
			return;

		if ( direction == WheelDirection.None )
			return;

		if ( PhysicsBody != null && Joint.IsValid() )
		{
			var trq = PhysicsBody.Transform.NormalToWorld( torque * delta );

			if ( direction == WheelDirection.CounterClockwise )
				trq *= -1;

			PhysicsBody.ApplyTorque( trq * PhysicsBody.Mass );
		}
	}

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

	private WheelDirection NextDirection( WheelDirection curDirection )
	{
		switch ( curDirection )
		{
			case WheelDirection.None:
				return WheelDirection.Clockwise;
			case WheelDirection.Clockwise:
				return WheelDirection.CounterClockwise;
			default:
				return WheelDirection.None;
		}
	}

	public bool OnUse( Entity user )
	{
		direction = NextDirection( direction );

		return false;
	}

	public bool IsUsable( Entity user )
	{
		//return user == Owner;
		return true;
	}
}
