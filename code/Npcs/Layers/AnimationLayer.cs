namespace Sandbox.Npcs.Layers;

/// <summary>
/// Provides animation parameters and helpers for behaviors.
/// Also handles look-at (eyes/head) and body turning via animator parameters.
/// </summary>
public sealed class AnimationLayer : BaseNpcLayer
{
	// Movement animation
	public float Speed { get; set; } = 1.0f;
	public bool IsGrounded { get; set; } = true;
	public float LookSpeed { get; set; } = 8f;
	public float MaxHeadAngle { get; set; } = 45f;

	/// <summary>
	/// Current world-space target the Npc is looking at (if any).
	/// </summary>
	public Vector3? LookTarget { get; private set; }

	private SkinnedModelRenderer _renderer;
	private float _lastYaw = float.NaN;

	protected override void OnStart()
	{
		_renderer = Npc.GetComponentInChildren<SkinnedModelRenderer>();
		_lastYaw = float.NaN;
	}

	protected override void OnUpdate()
	{
		if ( LookTarget.HasValue )
		{
			UpdateLookDirection( LookTarget.Value );
		}
	}

	/// <summary>
	/// Set both eye and head aim using a single local-space direction.
	/// </summary>
	public void SetAim( Vector3 localDirection )
	{
		_renderer?.Set( "aim_eyes", localDirection );
		_renderer?.Set( "aim_head", localDirection );
	}

	public void SetHead( Vector3 localDirection )
	{
		_renderer?.Set( "aim_head", localDirection );
	}

	public void SetEyes( Vector3 localDirection )
	{
		_renderer?.Set( "aim_eyes", localDirection );
	}

	public void SetMove( Vector3 velocity, Rotation reference )
	{
		if ( _renderer is null ) return;

		var forward = reference.Forward.Dot( velocity );
		var sideward = reference.Right.Dot( velocity );
		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		// Compute rotational speed around yaw (degrees per second)
		var yaw = reference.Angles().yaw.NormalizeDegrees();
		float rotationSpeed = 0.0f;

		if ( float.IsNaN( _lastYaw ) )
		{
			_lastYaw = yaw; // initialize history, no spike on first sample
		}
		else
		{
			var deltaYaw = Angles.NormalizeAngle( yaw - _lastYaw );
			rotationSpeed = Time.Delta > 0.0f ? MathF.Abs( deltaYaw ) / Time.Delta : 0.0f;
			_lastYaw = yaw;
		}

		_renderer.Set( "move_direction", angle );
		_renderer.Set( "move_speed", velocity.Length );
		_renderer.Set( "move_groundspeed", velocity.WithZ( 0 ).Length );
		_renderer.Set( "move_y", sideward );
		_renderer.Set( "move_x", forward );
		_renderer.Set( "move_z", velocity.z );
		_renderer.Set( "b_grounded", IsGrounded );
		_renderer.Set( "speed_move", Speed );
		_renderer.Set( "move_rotationspeed", rotationSpeed );
	}

	public void TriggerAttack()
	{
		if ( _renderer is null ) return;

		_renderer.Set( "b_attack", true );
	}

	/// <summary>
	/// Command this layer to look at a target
	/// </summary>
	public void LookAt( Vector3 target )
	{
		LookTarget = target;
	}

	/// <summary>
	/// Stop looking
	/// </summary>
	public void StopLooking()
	{
		LookTarget = null;

		if ( _renderer is not null )
		{
			_renderer.Set( "aim_eyes", Vector3.Zero );
			_renderer.Set( "aim_head", Vector3.Zero );
		}
	}

	/// <summary>
	/// Check if we're facing the target sufficiently.
	/// </summary>
	public bool IsFacingTarget()
	{
		if ( !LookTarget.HasValue ) return true;
		if ( _renderer is null ) return true;

		var direction = (LookTarget.Value.WithZ( 0 ) - Npc.WorldPosition.WithZ( 0 )).Normal;
		var dot = Npc.WorldRotation.Forward.WithZ( 0 ).Dot( direction );
		return dot > 0.90f;
	}

	/// <summary>
	/// Update look direction - handles head/eye tracking and rotates the body when needed.
	/// </summary>
	private void UpdateLookDirection( Vector3 targetPosition )
	{
		if ( _renderer is null ) return;

		var worldDirection = ((targetPosition - Npc.WorldPosition) with { z = 0 }).Normal;
		var currentForward = Npc.WorldRotation.Forward;

		var angleToTarget = Vector3.GetAngle( currentForward, worldDirection );

		var localDirection = Npc.WorldRotation.Inverse * worldDirection;

		_renderer.Set( "aim_head", localDirection );
		_renderer.Set( "aim_eyes", localDirection );

		//if ( angleToTarget > MaxHeadAngle || Speed > 1 )
		{
			var targetRotation = Rotation.LookAt( worldDirection, Vector3.Up );
			var t = LookSpeed * Time.Delta;
			Npc.GameObject.WorldRotation = Rotation.Lerp( Npc.WorldRotation, targetRotation, t );
		}
	}

	public override void Reset()
	{
		if ( _renderer is null ) return;

		IsGrounded = false;
		Speed = 1.0f;
		LookTarget = null;
		_lastYaw = float.NaN;

		_renderer.Set( "b_attack", false );
		_renderer.Set( "move_speed", 0.0f );
		_renderer.Set( "move_groundspeed", 0.0f );
		_renderer.Set( "move_y", 0.0f );
		_renderer.Set( "move_x", 0.0f );
		_renderer.Set( "move_z", 0.0f );
		_renderer.Set( "b_grounded", false );
		_renderer.Set( "speed_move", 1.0f );
		_renderer.Set( "move_rotationspeed", 0.0f );

		_renderer.Set( "aim_eyes", Vector3.Zero );
		_renderer.Set( "aim_head", Vector3.Zero );
	}
}
