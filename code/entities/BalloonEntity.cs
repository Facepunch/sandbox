using Sandbox;

[Library( "ent_balloon", Title = "Balloon", Spawnable = true )]
public partial class BalloonEntity : Prop
{
	private static float GravityScale => -0.2f;

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/citizen_props/balloonregular01.vmdl" );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
		PhysicsBody.GravityScale = GravityScale;
		RenderColor = Color.Random;
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
}
