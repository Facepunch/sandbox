
[Hide]
public class Thruster : Component
{
	[Range( 0, 1 )]
	public float Power { get; set; } = 0.5f;

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( Input.Down( "jump" ) )
		{
			AddThrust( 1 );
		}
	}

	void AddThrust( float amount )
	{
		var body = GetComponent<Rigidbody>();
		if ( body == null ) return;

		body.ApplyImpulse( WorldRotation.Up * -10000 * amount * Power );
	}

}
