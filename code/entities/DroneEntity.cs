using Sandbox;
using System;

[Library( "ent_drone", Title = "Drone", Spawnable = true )]
public partial class DroneEntity : Prop
{
	public virtual float altitudeAcceleration => 2000;
	public virtual float movementAcceleration => 5000;
	public virtual float yawSpeed => 150;
	public virtual float uprightSpeed => 5000;
	public virtual float uprightDot => 0.5f;
	public virtual float leanWeight => 0.5f;
	public virtual float leanMaxVelocity => 1000;

	private struct DroneInputState
	{
		public Vector3 movement;
		public float throttle;
		public float pitch;
		public float yaw;

		public void Reset()
		{
			movement = Vector3.Zero;
			pitch = 0;
			yaw = 0;
		}
	}

	private DroneInputState currentInput;

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "entities/drone/drone.vmdl" );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
	}

	[Event.Physics.PostStep]
	protected void ApplyForces()
	{
		if ( !PhysicsBody.IsValid() )
		{
			return;
		}

		var body = PhysicsBody;
		var transform = Transform;

		body.LinearDrag = 1.0f;
		body.AngularDrag = 1.0f;
		body.LinearDamping = 4.0f;
		body.AngularDamping = 4.0f;

		var yawRot = Rotation.From( new Angles( 0, Rotation.Angles().yaw, 0 ) );
		var worldMovement = yawRot * currentInput.movement;
		var velocityDirection = body.Velocity.WithZ( 0 );
		var velocityMagnitude = velocityDirection.Length;
		velocityDirection = velocityDirection.Normal;

		var velocityScale = (velocityMagnitude / leanMaxVelocity).Clamp( 0, 1 );
		var leanDirection = worldMovement.LengthSquared == 0.0f
			? -velocityScale * velocityDirection
			: worldMovement;

		var targetUp = (Vector3.Up + leanDirection * leanWeight * velocityScale).Normal;
		var currentUp = transform.NormalToWorld( Vector3.Up );
		var alignment = Math.Max( Vector3.Dot( targetUp, currentUp ), 0 );

		bool hasCollision = false;
		bool isGrounded = false;

		if ( !hasCollision || isGrounded )
		{
			var hoverForce = isGrounded && currentInput.throttle <= 0 ? Vector3.Zero : -1 * transform.NormalToWorld( Vector3.Up ) * -800.0f;
			var movementForce = isGrounded ? Vector3.Zero : worldMovement * movementAcceleration;
			var altitudeForce = transform.NormalToWorld( Vector3.Up ) * currentInput.throttle * altitudeAcceleration;
			var totalForce = hoverForce + movementForce + altitudeForce;
			body.ApplyForce( (totalForce * alignment) * body.Mass );
		}

		if ( !hasCollision && !isGrounded )
		{
			var spinTorque = Transform.NormalToWorld( new Vector3( 0, 0, currentInput.yaw * yawSpeed ) );
			var uprightTorque = Vector3.Cross( currentUp, targetUp ) * uprightSpeed;
			var uprightAlignment = alignment < uprightDot ? 0 : alignment;
			var totalTorque = spinTorque * alignment + uprightTorque * uprightAlignment;
			body.ApplyTorque( (totalTorque * alignment) * body.Mass );
		}
	}

	public override void Simulate( Client owner )
	{
		if ( owner == null ) return;
		if ( !IsServer ) return;

		using ( Prediction.Off() )
		{
			currentInput.Reset();
			var x = (Input.Down( InputButton.Forward ) ? -1 : 0) + (Input.Down( InputButton.Back ) ? 1 : 0);
			var y = (Input.Down( InputButton.Right ) ? 1 : 0) + (Input.Down( InputButton.Left ) ? -1 : 0);
			currentInput.movement = new Vector3( x, y, 0 ).Normal;
			currentInput.throttle = (Input.Down( InputButton.Run ) ? 1 : 0) + (Input.Down( InputButton.Duck ) ? -1 : 0);
			currentInput.yaw = -Input.MouseDelta.x;
		}
	}

	public void ResetInput()
	{
		currentInput.Reset();
	}

	private readonly Vector3[] turbinePositions = new Vector3[]
	{
		new Vector3( -35.37f, 35.37f, 10.0f ),
		new Vector3( 35.37f, 35.37f, 10.0f ),
		new Vector3( 35.37f, -35.37f, 10.0f ),
		new Vector3( -35.37f, -35.37f, 10.0f )
	};

	public override void OnNewModel( Model model )
	{
		base.OnNewModel( model );

		if ( IsClient )
		{
		}
	}

	private float spinAngle;

	[Event.Frame]
	public void OnFrame()
	{
		spinAngle += 10000.0f * Time.Delta;
		spinAngle %= 360.0f;

		for ( int i = 0; i < turbinePositions.Length; ++i )
		{
			var transform = Transform.ToWorld( new Transform( turbinePositions[i] * Scale, Rotation.From( new Angles( 0, spinAngle, 0 ) ) ) );
			transform.Scale = Scale;
			SetBoneTransform( i, transform );
		}
	}
}
