using Sandbox;

[Library( "ent_thruster" )]
public partial class ThrusterEntity : Prop, IPhysicsUpdate, IUse
{
	public float Force = 1000.0f;
	public bool Massless = false;
	public PhysicsBody TargetBody;

	[Net]
	public bool Enabled { get; set; } = true;

	public virtual void OnPostPhysicsStep( float dt )
	{
		if ( IsServer && Enabled )
		{
			if ( TargetBody.IsValid() )
			{
				TargetBody.ApplyForceAt( WorldPos, WorldRot.Down * (Massless ? Force * TargetBody.Mass : Force) );
			}
			else if ( PhysicsBody.IsValid() )
			{
				PhysicsBody.ApplyForce( WorldRot.Down * (Massless ? Force * PhysicsBody.Mass : Force) );
			}
		}
	}

	public bool IsUsable( Entity user )
	{
		return true;
	}

	public bool OnUse( Entity user )
	{
		Enabled = !Enabled;

		return false;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( IsClient )
		{
			KillEffects();
		}
	}
}
