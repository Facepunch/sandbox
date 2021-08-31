using Sandbox;
using System.Linq;

[Library( "directional_gravity", Title = "Directional Gravity", Spawnable = true )]
public partial class DirectionalGravity : Prop
{
	bool enabled = false;

	public override void Spawn()
	{
		base.Spawn();

		DeleteOthers();

		SetModel( "models/arrow.vmdl" );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );

		enabled = true;
	}

	private void DeleteOthers()
	{
		// Only allow one of these to be spawned at a time
		foreach ( var ent in All.OfType<DirectionalGravity>()
			.Where( x => x.IsValid() && x != this ) )
		{
			ent.Delete();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( IsServer )
		{
			PhysicsWorld.UseDefaultGravity();
			PhysicsWorld.WakeAllBodies();
		}

		enabled = false;
	}

	[Event.Physics.PostStep]
	protected void UpdateGravity()
	{
		if ( !IsServer )
			return;

		if ( !enabled )
			return;

		if ( !this.IsValid() )
			return;

		var gravity = Rotation.Down * 800.0f;

		if ( gravity != PhysicsWorld.Gravity )
		{
			PhysicsWorld.Gravity = gravity;
			PhysicsWorld.WakeAllBodies();
		}
	}
}
