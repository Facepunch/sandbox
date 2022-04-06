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

		Map.Physics.Gravity = Vector3.Down * 800.0f;

		enabled = false;
	}

	[Event.Tick]
	protected void UpdateGravity()
	{
		if ( !IsServer )
			return;

		if ( !enabled )
			return;

		if ( !this.IsValid() )
			return;

		Map.Physics.Gravity = Rotation.Down * 800.0f;
	}
}
