using Sandbox.Citizen;

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

	/// <summary>
	/// The prop currently being held, if any.
	/// </summary>
	public GameObject HeldProp => _heldProp;

	private SkinnedModelRenderer _renderer;
	private float _lastYaw = float.NaN;
	private GameObject _heldProp;
	private float _holdPose;
	private bool _oneHanded;

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
	/// Pick up and hold a prop — disables physics
	///
	/// holdtype_pose ranges:
	///   0-2 : close grip (~16u out), interpolates weight poses — used for heavy objects, but small enough to hold close
	///   2-4 : arms extend outwards — used for normal objects, mapped by width
	///   4-5 : above the head — used for large objects
	/// </summary>
	public void SetHeldProp( GameObject prop )
	{
		if ( !prop.IsValid() ) return;

		_heldProp = prop;

		// Grab mass before disabling physics
		var rb = prop.GetComponent<Rigidbody>( true );
		var mass = rb?.Mass ?? 1f;

		if ( rb.IsValid() )
			rb.Enabled = false;

		// Measure the object
		var bounds = prop.GetBounds();
		var size = bounds.Size;
		var width = MathF.Max( size.x, size.y );
		var diagonal = size.Length;

		// Determine pose and hold offset from object properties
		Vector3 holdOffset;
		var holdRotation = Npc.WorldRotation;

		// Small, light objects can be held one-handed
		_oneHanded = diagonal < 32f && mass <= 128;

		// TODO: too many magic numbers 

		if ( diagonal >= 64f )
		{
			// Large — above the citizen's head (pose 4-5)
			var t = ((diagonal - 64f) / 64f).Clamp( 0f, 1f );
			_holdPose = 4f + t;
			holdOffset = Vector3.Up * 66f + Npc.WorldRotation.Forward * 4f;

			// Orient the long axis forward so it doesn't stick out sideways
			prop.WorldRotation = holdRotation;
			var heldSize = prop.GetBounds().Size;
			var left = holdRotation.Left;
			var fwd = holdRotation.Forward;
			var sideExtent = MathF.Abs( heldSize.x * left.x ) + MathF.Abs( heldSize.y * left.y );
			var fwdExtent = MathF.Abs( heldSize.x * fwd.x ) + MathF.Abs( heldSize.y * fwd.y );

			if ( sideExtent > fwdExtent * 1.2f )
			{
				holdRotation *= Rotation.FromAxis( Vector3.Up, 90f );
			}
		}
		else if ( mass > 128 )
		{
			// Heavy — close grip (pose 0-2)
			var t = ((mass - 30f) / 170f).Clamp( 0f, 1f );
			_holdPose = t * 2f;
			holdOffset = Npc.WorldRotation.Forward * 8f + Vector3.Up * 30f;
		}
		else
		{
			// Normal — arms extend by width (pose 2-4, distance 16-32)
			var t = (width / 32f).Clamp( 0f, 1f );
			_holdPose = 2f + t * 2f;
			var forwardDist = 8 + t * 8f;
			holdOffset = Npc.WorldRotation.Forward * forwardDist + Vector3.Up * 30f;
		}

		// One-handed: parent directly to the right hand bone
		// Two-handed: parent to spine so it sways with the walk cycle
		GameObject parent;

		if ( _oneHanded )
		{
			var handBone = _renderer?.GetBoneObject( "hold_R" );
			parent = handBone ?? Npc.GameObject;

			prop.WorldPosition = parent.WorldPosition;
			prop.WorldRotation = holdRotation;
			prop.SetParent( parent, true );
		}
		else
		{
			var bone = _renderer?.GetBoneObject( "spine_2" );
			parent = bone ?? Npc.GameObject;

			prop.WorldPosition = Npc.WorldPosition + holdOffset;
			prop.WorldRotation = holdRotation;
			prop.SetParent( parent, true );
		}

		_renderer?.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.HoldItem );
		_renderer?.Set( "holdtype_pose", _holdPose );
		_renderer?.Set( "holdtype_pose_hand", 0.005f );
		_renderer?.Set( "holdtype_handedness", (int)(_oneHanded ? CitizenAnimationHelper.Hand.Right : CitizenAnimationHelper.Hand.Left) );
	}

	/// <summary>
	/// Drop the held prop — clears IK, holdtype, holdtype_pose, places the prop
	/// on the ground in front of the NPC, unparents, and re-enables physics.
	/// Safe to call when nothing is held.
	/// </summary>
	public void ClearHeldProp()
	{
		if ( _renderer is not null )
		{
			_renderer.Set( "holdtype", 0 );
			_renderer.Set( "holdtype_pose", 0f );
			_renderer.Set( "holdtype_handedness", 0 );
			_renderer.ClearIk( "hand_right" );
			_renderer.ClearIk( "hand_left" );
		}

		if ( _heldProp.IsValid() )
		{
			// Use the prop's forward extent + padding so it lands clear of the Npc
			var bounds = _heldProp.GetBounds();
			var fwd = Npc.WorldRotation.Forward;
			var forwardExtent = MathF.Abs( bounds.Extents.x * fwd.x )
								+ MathF.Abs( bounds.Extents.y * fwd.y );
			var dropDist = forwardExtent + 12f;

			var dropPos = Npc.WorldPosition
						  + fwd * dropDist
						  + Vector3.Up * bounds.Extents.z;

			_heldProp.WorldPosition = dropPos;
			_heldProp.WorldRotation = Npc.WorldRotation;
			_heldProp.SetParent( null, true );

			if ( _heldProp.GetComponent<Rigidbody>( true ) is { } rb )
				rb.Enabled = true;
		}

		_heldProp = null;
		_holdPose = 0f;
		_oneHanded = false;
	}

	/// <summary>
	/// Update IK hand targets each frame from the held prop's bounds.
	/// If the object is too wide to grip from the sides, support from below with palms up.
	/// </summary>
	private void UpdateHeldPropIk()
	{
		if ( _renderer is null || !_heldProp.IsValid() ) return;

		if ( _oneHanded )
		{
			_renderer.ClearIk( "hand_right" );
			_renderer.ClearIk( "hand_left" );
			return;
		}

		var bounds = _heldProp.GetBounds();
		var center = bounds.Center;
		var forward = _heldProp.WorldRotation.Forward;
		var left = _heldProp.WorldRotation.Left;

		var halfSpread = MathF.Max( MathF.Max( bounds.Extents.x, bounds.Extents.y ), 12f );

		Rotation rightRot;
		Rotation leftRot;
		Vector3 rightHandPos;
		Vector3 leftHandPos;

		if ( halfSpread > 24 )
		{
			// Too wide to grip from the sides — support from below, palms up
			rightHandPos = center - left * halfSpread + Vector3.Down * bounds.Extents.z;
			leftHandPos = center + left * halfSpread + Vector3.Down * bounds.Extents.z;

			rightRot = Rotation.LookAt( forward, Vector3.Down );
			leftRot = Rotation.LookAt( forward, Vector3.Up );
		}
		else
		{
			// Narrow enough to grip from the sides, palms inward
			rightHandPos = center - left * halfSpread;
			leftHandPos = center + left * halfSpread;

			rightRot = Rotation.LookAt( forward, -left );
			leftRot = Rotation.LookAt( forward, -left );
		}

		_renderer.SetIk( "hand_right", new Transform( rightHandPos, rightRot ) );
		_renderer.SetIk( "hand_left", new Transform( leftHandPos, leftRot ) );
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
