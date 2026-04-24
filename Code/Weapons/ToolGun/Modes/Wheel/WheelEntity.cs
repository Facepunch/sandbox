public class WheelEntity : Component, IPlayerControllable
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

	Vector3 _localAxle;
	Vector3 _localUp;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		var joint = GetComponentInChildren<WheelJoint>();
		if ( joint.IsValid() && joint.Body.IsValid() )
		{
			var bodyRotInv = joint.Body.WorldRotation.Inverse;
			_localAxle = bodyRotInv * WorldRotation.Right;
			_localUp = bodyRotInv * WorldRotation.Up;
		}
	}

	protected override void OnUpdate()
	{
		if ( SpawnMenuHost.GetActiveMode() is not ContextMenuHost )
			return;

		var renderers = GameObject.GetComponentsInChildren<Renderer>();
		var outlines = Game.ActiveScene.GetAllComponents<HighlightOutline>();
		if ( !outlines.Any( o => o.Targets?.Any( t => renderers.Contains( t ) ) == true ) )
			return;

		var joint = GetComponentInChildren<WheelJoint>();
		if ( !joint.IsValid() || !joint.Body.IsValid() || _localAxle == Vector3.Zero )
			return;

		var spinAxis = joint.Body.WorldRotation * _localAxle;
		var stableUp = joint.Body.WorldRotation * _localUp;

		var renderer = GameObject.GetComponentInChildren<ModelRenderer>();
		var overlayRadius = 0f;
		if ( renderer.IsValid() )
		{
			var size = renderer.Model.Bounds.Size;
			overlayRadius = MathF.Max( size.x, size.z ) * 0.01f * WorldScale.x;
		}

		WheelOverlay.DrawDirection( WorldPosition, spinAxis, stableUp, overlayRadius, Reversed );
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

