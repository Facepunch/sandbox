using Sandbox.Citizen;

namespace Sandbox.Npcs.Layers;

/// <summary>
/// Provides animation parameters and helpers for behaviors.
/// Also handles look-at (eyes/head) and body turning via animator parameters.
/// Synced properties replicate animation state to all clients.
/// </summary>
public sealed partial class AnimationLayer : BaseNpcLayer
{
	public float Speed { get; set; } = 1.0f;
	public bool IsGrounded { get; set; } = true;
	public float LookSpeed { get; set; } = 3f;
	public float MaxHeadAngle { get; set; } = 45f;

	public float AimStrengthEyes { get; set; } = 1.0f;
	public float AimStrengthHead { get; set; } = 1.0f;
	public float AimStrengthBody { get; set; } = 1.0f;

	/// <summary>
	/// Current world-space target the Npc is looking at (if any). Host-only.
	/// </summary>
	public Vector3? LookTarget { get; private set; }

	/// <summary>
	/// The GameObject being tracked as the look target, if any. Host-only.
	/// </summary>
	public GameObject LookTargetObject { get; private set; }

	private SkinnedModelRenderer _renderer => Npc.Renderer;
	private float _lastYaw = float.NaN;

	[Sync] public Vector3 MoveVelocity { get; set; }
	[Sync] public Rotation MoveRotation { get; set; }
	[Sync] public bool Grounded { get; set; } = true;
	[Sync] public Vector3 LookWorldPos { get; set; }
	[Sync] public bool IsLooking { get; set; }
	[Sync] public int HoldType { get; set; }

	protected override void OnStart()
	{
		_lastYaw = float.NaN;
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			if ( LookTargetObject.IsValid() )
				LookTarget = LookTargetObject.WorldPosition;

			IsLooking = LookTarget.HasValue;
			if ( LookTarget.HasValue )
			{
				LookWorldPos = LookTarget.Value;
				UpdateLookDirection( LookTarget.Value );
			}

			if ( _heldProp.IsValid() )
				UpdateHeldPropIk();
		}
		else
		{
			if ( IsLooking )
				ApplyLookToRenderer( LookWorldPos );
		}

		ApplyMoveToRenderer( MoveVelocity, MoveRotation );
		_renderer?.Set( "holdtype", HoldType );
		_renderer?.Set( "b_grounded", Grounded );
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
		IsLooking = false;

		_renderer?.SetLookDirection( "aim_eyes", Vector3.Zero, 0f );
		_renderer?.SetLookDirection( "aim_head", Vector3.Zero, 0f );
		_renderer?.SetLookDirection( "aim_body", Vector3.Zero, 0f );
	}

	/// <summary>
	/// Command this layer to look at a target (one-shot, no tracking).
	/// </summary>
	public void LookAt( Vector3 target ) => LookTarget = target;

	/// <summary>Stop looking.</summary>
	public void StopLooking() => ClearLookTarget();

	/// <summary>
	/// Returns true if the NPC body is facing the current look target within MaxHeadAngle.
	/// </summary>
	public bool IsFacingTarget()
	{
		if ( !LookTarget.HasValue ) return true;
		if ( _renderer is null ) return true;

		var direction = (LookTarget.Value.WithZ( 0 ) - Npc.WorldPosition.WithZ( 0 )).Normal;
		var angleToTarget = Vector3.GetAngle( Npc.WorldRotation.Forward.WithZ( 0 ), direction );
		return angleToTarget <= MaxHeadAngle;
	}

	private void UpdateLookDirection( Vector3 targetPosition )
	{
		if ( _renderer is null ) return;

		var fullDirection = (targetPosition - Npc.WorldPosition).Normal;
		var flatDirection = (targetPosition - Npc.WorldPosition).WithZ( 0 ).Normal;

		_renderer.SetLookDirection( "aim_eyes", fullDirection, AimStrengthEyes );
		_renderer.SetLookDirection( "aim_head", fullDirection, AimStrengthHead );
		_renderer.SetLookDirection( "aim_body", fullDirection, AimStrengthBody );

		var angleToTarget = Vector3.GetAngle( Npc.WorldRotation.Forward, flatDirection );

		if ( angleToTarget > MaxHeadAngle )
		{
			var targetRotation = Rotation.LookAt( flatDirection, Vector3.Up );
			Npc.GameObject.WorldRotation = Rotation.Lerp( Npc.WorldRotation, targetRotation, LookSpeed * Time.Delta );
		}
	}

	private void ApplyLookToRenderer( Vector3 lookWorldPos )
	{
		if ( _renderer is null ) return;

		var fullDirection = (lookWorldPos - Npc.WorldPosition).Normal;

		_renderer.SetLookDirection( "aim_eyes", fullDirection, AimStrengthEyes );
		_renderer.SetLookDirection( "aim_head", fullDirection, AimStrengthHead );
		_renderer.SetLookDirection( "aim_body", fullDirection, AimStrengthBody );
	}

	public void SetAim( Vector3 direction )
	{
		_renderer?.SetLookDirection( "aim_eyes", direction, AimStrengthEyes );
		_renderer?.SetLookDirection( "aim_head", direction, AimStrengthHead );
		_renderer?.SetLookDirection( "aim_body", direction, AimStrengthBody );
	}

	public void SetHead( Vector3 direction ) => _renderer?.SetLookDirection( "aim_head", direction, AimStrengthHead );
	public void SetEyes( Vector3 direction ) => _renderer?.SetLookDirection( "aim_eyes", direction, AimStrengthEyes );

	/// <summary>
	/// Records move state for replication. Called by NavigationLayer on the host.
	/// All clients apply this each frame in OnUpdate.
	/// </summary>
	public void SetMove( Vector3 velocity, Rotation reference )
	{
		MoveVelocity = velocity;
		MoveRotation = reference;
	}

	private void ApplyMoveToRenderer( Vector3 velocity, Rotation reference )
	{
		if ( _renderer is null ) return;
		if ( reference.w == 0f ) return;

		var forward = reference.Forward.Dot( velocity );
		var sideward = reference.Right.Dot( velocity );
		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		var yaw = reference.Angles().yaw.NormalizeDegrees();
		float rotationSpeed = 0f;

		if ( float.IsNaN( _lastYaw ) )
		{
			_lastYaw = yaw;
		}
		else
		{
			var deltaYaw = Angles.NormalizeAngle( yaw - _lastYaw );
			rotationSpeed = Time.Delta > 0f ? MathF.Abs( deltaYaw ) / Time.Delta : 0f;
			_lastYaw = yaw;
		}

		_renderer.Set( "move_direction", angle );
		_renderer.Set( "move_speed", velocity.Length );
		_renderer.Set( "move_groundspeed", velocity.WithZ( 0 ).Length );
		_renderer.Set( "move_y", sideward );
		_renderer.Set( "move_x", forward );
		_renderer.Set( "move_z", velocity.z );
		_renderer.Set( "speed_move", Speed );
		_renderer.Set( "move_rotationspeed", rotationSpeed );
	}

	/// <summary>
	/// Broadcasts the attack trigger to all clients so the animation plays everywhere.
	/// </summary>
	[Rpc.Broadcast]
	public void TriggerAttack()
	{
		_renderer?.Set( "b_attack", true );
	}

	/// <summary>
	/// Sets the holdtype so the NPC poses its arms for the held item.
	/// Synced to all clients via HoldType.
	/// </summary>
	public void SetHoldType( CitizenAnimationHelper.HoldTypes holdType )
	{
		HoldType = (int)holdType;
	}

	public override void ResetLayer()
	{
		if ( _renderer is null ) return;

		IsGrounded = false;
		Speed = 1.0f;
		LookTarget = null;
		LookTargetObject = null;
		IsLooking = false;
		MoveVelocity = default;
		HoldType = 0;
		_lastYaw = float.NaN;

		ClearHeldProp();

		_renderer.Set( "b_attack", false );
		_renderer.Set( "holdtype", 0 );
		_renderer.Set( "move_speed", 0f );
		_renderer.Set( "move_groundspeed", 0f );
		_renderer.Set( "move_y", 0f );
		_renderer.Set( "move_x", 0f );
		_renderer.Set( "move_z", 0f );
		_renderer.Set( "b_grounded", false );
		_renderer.Set( "speed_move", 1f );
		_renderer.Set( "move_rotationspeed", 0f );
		_renderer.SetLookDirection( "aim_eyes", Vector3.Zero, 0f );
		_renderer.SetLookDirection( "aim_head", Vector3.Zero, 0f );
		_renderer.SetLookDirection( "aim_body", Vector3.Zero, 0f );
	}
}
