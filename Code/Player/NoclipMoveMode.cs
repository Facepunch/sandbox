using Sandbox.Movement;

public sealed class NoclipMoveMode : Sandbox.Movement.MoveMode
{
	/// <summary>
	/// If true, the player will still collide with the world and other players. This probably
	/// means that the noclip mode is named wrong. But it's cool. It just becomes a fly around mode.
	/// </summary>
	[Property]
	public bool EnableCollision { get; set; }

	[Property]
	public float RunSpeed { get; set; } = 1200;

	[Property]
	public float WalkSpeed { get; set; } = 200;


	protected override void OnUpdateAnimatorState( SkinnedModelRenderer renderer )
	{
		renderer.Set( "b_noclip", true );
	}

	public override int Score( PlayerController controller )
	{
		return 1000;
	}

	public override void UpdateRigidBody( Rigidbody body )
	{
		body.Gravity = false;
		body.LinearDamping = 5.0f;
		body.AngularDamping = 1f;

		body.Tags.Set( "noclip", !EnableCollision );
	}

	public override void OnModeBegin()
	{
		Controller.IsClimbing = true;
		Controller.Body.Gravity = false;
	}

	public override void OnModeEnd( MoveMode next )
	{
		Controller.IsClimbing = false;
		Controller.Body.Velocity = Controller.Body.Velocity.ClampLength( Controller.RunSpeed );
		Controller.Body.Tags.Set( "noclip", false );
		Controller.Renderer.Set( "b_noclip", false );
	}

	public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
	{
		// don't normalize, because analog input might want to go slow
		input = input.ClampLength( 1 );

		var direction = eyes * input;

		// Run if we're holding down alt move button
		bool run = Input.Down( Controller.AltMoveButton );

		// if Run is default, flip that logic
		if ( Controller.RunByDefault ) run = !run;

		// if we're running, use run speed, if not use walk speed
		var velocity = run ? RunSpeed * 2.0f : RunSpeed;

		if ( direction.IsNearlyZero( 0.1f ) )
		{
			direction = 0;
		}

		// if we're hold down jump move upwards
		if ( Input.Down( "jump" ) ) direction += Vector3.Up;

		// if we're hold down duck move downwards
		if ( Input.Down( "duck" ) ) direction += Vector3.Down;

		return direction * velocity;
	}

}
