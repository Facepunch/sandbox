public class WheelEntity : Component, IPlayerControllable, IToolModePreview
{
	[Property, Range( 0, 1 ), ClientEditable]
	public bool Reversed { get; set; } = false;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Speed { get; set; } = 0.5f;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Power { get; set; } = 0.5f;

	[Property, Sync, ClientEditable]
	public ClientInput Forward { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput Reverse { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput Break { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput TurnLeft { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput TurnRight { get; set; }

	public void OnToolModePreview()
	{
		var tx = WorldTransform;
		var rollDir = Reversed ? -tx.Up : tx.Up;

		float faceOffset = 2f;
		var mr = GetComponentInChildren<ModelRenderer>();
		if ( mr != null )
		{
			var he = mr.Bounds.Size * 0.5f;
			var absRight = new Vector3( MathF.Abs( tx.Right.x ), MathF.Abs( tx.Right.y ), MathF.Abs( tx.Right.z ) );
			faceOffset = Vector3.Dot( he, absRight );
		}

		// TODO: better effects than this

		var center = tx.Position + tx.Right * faceOffset;
		var tip = center + rollDir * 14f;

		DebugOverlay.Line( new Line( center, tip ), Color.Cyan, 0f );

		var side = tx.Forward * 5f;
		var back = rollDir * 6f;
		DebugOverlay.Line( new Line( tip, tip - back + side ), Color.Cyan, 0f );
		DebugOverlay.Line( new Line( tip, tip - back - side ), Color.Cyan, 0f );
	}

	public void OnStartControl()
	{
	}

	public void OnEndControl()
	{
	}

	public void OnControl()
	{
		var joint = GetComponentInChildren<WheelJoint>();
		if ( !joint.IsValid() ) return;

		var forward = Forward.GetAnalog();
		var reverse = Reverse.GetAnalog();
		var speed = (forward - reverse).Clamp( -1, 1 );

		if ( Break.GetAnalog() > 0.1f )
		{
			joint.EnableSpinMotor = true;
			joint.SpinMotorSpeed = 0;
			joint.MaxSpinTorque = 500000 * Power;
		}
		else if ( speed.AlmostEqual( 0.0f ) )
		{
			joint.EnableSpinMotor = false;
		}
		else
		{
			if ( Reversed ) speed = -speed;

			joint.EnableSpinMotor = true;
			joint.SpinMotorSpeed = -2000 * speed * Speed;
			joint.MaxSpinTorque = 200000 * Power;
		}

		var left = TurnLeft.GetAnalog();
		var right = TurnRight.GetAnalog();
		var dir = (right - left).Clamp( -1, 1 );

		if ( !dir.AlmostEqual( 0.0f ) )
		{
			joint.EnableSteering = true;
			joint.SteeringDampingRatio = 1.0f;
			joint.MaxSteeringTorque = 500000;
			joint.SteeringLimits = new Vector2( -45, 45 );
			joint.TargetSteeringAngle = 30 * dir;
		}
		else
		{
			joint.TargetSteeringAngle = 0;
		}

	}
}

