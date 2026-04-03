using Sandbox.Citizen;

namespace Sandbox.Npcs.Layers;

/// <summary>
/// Provides animation parameters and helpers for behaviors.
/// Also handles look-at (eyes/head) and body turning via animator parameters.
/// </summary>
public sealed partial class AnimationLayer : BaseNpcLayer
{
	// Movement animation
	public float Speed { get; set; } = 1.0f;
	public bool IsGrounded { get; set; } = true;
	public float LookSpeed { get; set; } = 3f;
	public float MaxHeadAngle { get; set; } = 45f;

	/// <summary>
	/// Current world-space target the Npc is looking at (if any).
	/// Resolved each frame from either <see cref="LookTargetObject"/> or a fixed position.
	/// </summary>
	public Vector3? LookTarget { get; private set; }

	/// <summary>
	/// The GameObject being tracked as the look target, if any.
	/// </summary>
	public GameObject LookTargetObject { get; private set; }

	private SkinnedModelRenderer _renderer;
	private float _lastYaw = float.NaN;

	protected override void OnStart()
	{
		_renderer = Npc.GetComponentInChildren<SkinnedModelRenderer>();
		_lastYaw = float.NaN;
	}

	protected override void OnUpdate()
	{
		// Continuously resolve the look target from a tracked GameObject
		if ( LookTargetObject.IsValid() )
		{
			LookTarget = LookTargetObject.WorldPosition;
		}

		if ( LookTarget.HasValue )
		{
			UpdateLookDirection( LookTarget.Value );
		}

		if ( _heldProp.IsValid() )
		{
			UpdateHeldPropIk();
		}
	}

	/// <summary>
	/// Set a persistent look target that tracks a GameObject each frame.
	/// </summary>
	public void SetLookTarget( GameObject target )
	{
		LookTargetObject = target;
		LookTarget = target.IsValid() ? target.WorldPosition : null;
	}

	/// <summary>
	/// Set a persistent look target at a fixed world position.
	/// </summary>
	public void SetLookTarget( Vector3 target )
	{
		LookTargetObject = null;
		LookTarget = target;
	}

	/// <summary>
	/// Clear the persistent look target. The NPC will stop tracking.
	/// </summary>
	public void ClearLookTarget()
	{
		LookTargetObject = null;
		LookTarget = null;

		if ( _renderer is not null )
		{
			_renderer.Set( "aim_eyes", Vector3.Zero );
			_renderer.Set( "aim_head", Vector3.Zero );
		}
	}

	/// <summary>
	/// Command this layer to look at a target (one-shot, no tracking).
	/// Prefer <see cref="SetLookTarget(Vector3)"/> or <see cref="SetLookTarget(GameObject)"/> for persistent tracking.
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
		ClearLookTarget();
	}

	/// <summary>
	/// Check if we're facing the target sufficiently. Returns true if the target is within
	/// the head's comfortable range, since the head/eyes will handle the rest.
	/// </summary>
	public bool IsFacingTarget()
	{
		if ( !LookTarget.HasValue ) return true;
		if ( _renderer is null ) return true;

		var direction = (LookTarget.Value.WithZ( 0 ) - Npc.WorldPosition.WithZ( 0 )).Normal;
		var angleToTarget = Vector3.GetAngle( Npc.WorldRotation.Forward.WithZ( 0 ), direction );
		return angleToTarget <= MaxHeadAngle;
	}

	/// <summary>
	/// Update look direction - handles head/eye tracking and rotates the body only when the
	/// target is outside the comfortable head-turn range.
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

		// Only rotate the whole body when the head can't comfortably reach
		if ( angleToTarget > MaxHeadAngle )
		{
			var targetRotation = Rotation.LookAt( worldDirection, Vector3.Up );
			var t = LookSpeed * Time.Delta;
			Npc.GameObject.WorldRotation = Rotation.Lerp( Npc.WorldRotation, targetRotation, t );
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
	/// Sets the holdtype animation parameter so the NPC poses its arms correctly for the held item.
	/// </summary>
	public void SetHoldType( CitizenAnimationHelper.HoldTypes holdType )
	{
		_renderer?.Set( "holdtype", (int)holdType );
	}

	public override void Reset()
	{
		if ( _renderer is null ) return;

		IsGrounded = false;
		Speed = 1.0f;
		LookTarget = null;
		LookTargetObject = null;
		_lastYaw = float.NaN;

		ClearHeldProp();

		_renderer.Set( "b_attack", false );
		_renderer.Set( "holdtype", 0 );
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
