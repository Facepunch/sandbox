using Sandbox;
using System;

[Library( "ent_balloon", Title = "Balloon", Spawnable = true )]
public partial class BalloonEntity : Prop, ICanUndo
{
	private static float GravityScale => -0.2f;

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/citizen_props/balloonregular01.vmdl" );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
		PhysicsBody.GravityScale = GravityScale;
		RenderColor = Color.Random.ToColor32();
	}

	public override void OnKilled()
	{
		base.OnKilled();

		PlaySound( "balloon_pop_cute" );
	}

	[Event.Physics.PostStep]
	protected void UpdateGravity()
	{
		if ( !this.IsValid() )
			return;

		var body = PhysicsBody;
		if ( !body.IsValid() )
			return;


		body.GravityScale = GravityScale;
	}

	/*
	 * Implementation of the cancellation interface.
	 * This interface allows you to add the entire entity to the entire undo block, without the need to create an intermediary.
	 */
	public void DoUndo() => Delete();

	public bool IsValidUndo() => this.IsValid();
}
