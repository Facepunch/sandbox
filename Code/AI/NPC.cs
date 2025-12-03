namespace Sandbox.AI;

/// <summary>
/// The goal of this class is to provide a mini-framework for NPCs. Right now it's a simple state machine with perception, and a bunch of configurable properties.
/// </summary>
[Title( "NPC" ), Icon( "🥸" )]
public sealed partial class Npc : Component, IActor
{
	[RequireComponent] NavMeshAgent NavMeshAgent { get; set; }

	/// <summary>
	/// The body of the npc
	/// </summary>
	[Property, Group( "Body" )] public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// Where are their eyes?
	/// </summary>
	[Property, Group( "Body" )] public GameObject EyeSource { get; set; }

	/// <summary>
	/// Optionally spawn a weapon in the NPC's hands that they can use
	/// </summary>
	[Property] public GameObject WeaponPrefab { get; set; }

	/// <summary>
	/// The NPC's relationship to other NPCs and players
	/// </summary>
	[Property] public Relationship Relationship { get; set; } = Relationship.Neutral;

	/// <summary>
	/// How far away do we search for targets
	/// </summary>
	[Property, Group( "Skill" )] public float DetectionRange { get; set; } = 4096f;

	/// <summary>
	/// How far away can the NPC look at friendlies when idle
	/// </summary>
	[Property, Group( "Body" ), Range( 64f, 1024f )] public float IdleLookRange { get; set; } = 512f;

	/// <summary>
	/// The health threshold for a npc running away from its target  
	/// </summary>
	[Property, Group( "Skill" )] public float FleeThreshold { get; set; } = 25f;

	/// <summary>
	/// How far does the NPC flee
	/// </summary>
	[Property, Group( "Skill" )] public float FleeRange { get; set; } = 4096f;

	/// <summary>
	/// Constraint for the look pitch
	/// </summary>
	[Property, Group( "Body" )] public RangedFloat LookPitch = new( -45f, 45f );

	/// <summary>
	/// Constraint for the look yaw
	/// </summary>
	[Property, Group( "Body" )] public RangedFloat LookYaw = new( -60f, 60f );

	/// <summary>
	/// How fast the body turns to follow the eye target
	/// </summary>
	[Property, Range( 1, 16f ), Group( "Body" )] public float BodyTurnSpeed { get; set; } = 6f;

	/// <summary>
	/// Delay before body starts turning when idling (in seconds)
	/// </summary>
	[Property, Range( 0f, 2f ), Group( "Body" )] public float IdleBodyTurnDelay { get; set; } = 0.5f;

	/// <summary>
	/// NPC's aiming skill level (0.0 = terrible aim, 1.0 = perfect aim)
	/// </summary>
	[Property, Range( 0, 1 ), Step( 0.05f ), Group( "Skill" )] public float AimingSkill { get; set; } = 0.5f;

	/// <summary>
	/// How far away do we start shooting at a target -- this could probably be on the weapon
	/// </summary>
	[Property, Range( 256, 16834 ), Step( 1 ), Group( "Skill" )] private float AttackRange { get; set; } = 4096;

	/// <summary>
	/// If we're following a friendly, what's the desired distance away from them? The npc will try to abide by this
	/// </summary>
	[Property, Range( 64, 512f )] public float FollowDistance { get; set; } = 300f;

	/// <summary>
	/// Distance tolerance so they don't just go back and forth 
	/// </summary>
	[Property, Range( 4f, 64f )] public float FollowTolerance { get; set; } = 50f;

	// Tracking for delayed body turning
	TimeSince _timeSinceEyeTargetChanged;
	Vector3? _previousEyeTarget;
	Vector3? _eyeTarget;

	protected override void OnStart()
	{
		if ( IsProxy )
			return;

		if ( WeaponPrefab is null )
			return;

		var go = WeaponPrefab.Clone();
		go.SetParent( GameObject, false );

		_weapon = go.GetComponent<BaseCarryable>();
		_weapon.CreateWorldModel( Renderer );
	}

	protected override void OnDestroy()
	{
		if ( IsProxy )
			return;

		CancelTasks();
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			UpdatePerception();
			UpdateEyeTarget();
			UpdateEyeSystem();
			UpdateState();
		}

		UpdateAnimation();
	}

	/// <summary>
	/// Implements IActor.EyeTransform
	/// </summary>
	public Transform EyeTransform => EyeSource.WorldTransform;

	/// <summary>
	/// Sets the world position for the NPC to look at
	/// </summary>
	/// <param name="worldPosition">World position to look at, or null to clear target</param>
	public void SetEyeTarget( Vector3? worldPosition )
	{
		// Track when the eye target changes
		if ( _eyeTarget != worldPosition )
		{
			_previousEyeTarget = _eyeTarget;
			_timeSinceEyeTargetChanged = 0f;
		}

		_eyeTarget = worldPosition;
	}

	private void UpdateEyeTarget()
	{
		Vector3? newTarget = null;

		if ( _currentState == State.Idle )
		{
			var friend = FindClosestWithinRange( _friends, IdleLookRange );
			if ( friend.IsValid() )
				newTarget = GetEye( friend );
		}
		else if ( _currentTarget.IsValid() )
		{
			if ( _currentState == State.Attack )
			{
				var targetEye = GetEye( _currentTarget );
				var distance = DistanceTo( _currentTarget );
				newTarget = CalculateAimVector( targetEye, distance );
			}
			else
			{
				newTarget = GetEye( _currentTarget );
			}
		}

		SetEyeTarget( newTarget );
	}

	private void UpdateEyeSystem()
	{
		if ( _eyeTarget is null )
		{
			// No rotation when no target
			_currentRotationSpeed = 0f;
			return;
		}

		var eyePosition = EyeTransform.Position;
		var targetPosition = _eyeTarget.Value;
		targetPosition = targetPosition.WithZ( eyePosition.z );

		var lookDirection = (targetPosition - eyePosition).Normal;

		if ( lookDirection.IsNearlyZero() )
		{
			_currentRotationSpeed = 0f;
			return;
		}

		// Always update head and eye look immediately
		var headLookDirection = lookDirection;

		// Update head and eye look using Vector3 parameters
		if ( Renderer.IsValid() )
		{
			// Project the head look direction forward by 1024 units to prevent steep upward angles for close objects
			var localTargetPosition = WorldTransform.PointToLocal( eyePosition + headLookDirection * 1024f );

			Renderer.Set( "aim_head", localTargetPosition.Normal );
			Renderer.Set( "aim_eyes", localTargetPosition.Normal );
		}

		EyeSource.WorldRotation = Rotation.LookAt( headLookDirection );

		// Handle body turning with delay for idle state
		var desiredBodyDirection = lookDirection.WithZ( 0 ).Normal;

		if ( !desiredBodyDirection.IsNearlyZero() )
		{
			var desiredBodyRotation = Rotation.LookAt( desiredBodyDirection, Vector3.Up );
			var currentYaw = WorldRotation.Yaw();
			var desiredYaw = desiredBodyRotation.Yaw();
			var yawDifference = Angles.NormalizeAngle( desiredYaw - currentYaw );

			// Only rotate if the difference is significant enough
			if ( MathF.Abs( yawDifference ) > 5f )
			{
				bool shouldTurnBody = true;

				// Apply delay for body turning when idling
				if ( _currentState == State.Idle )
				{
					shouldTurnBody = _timeSinceEyeTargetChanged >= IdleBodyTurnDelay;
				}

				if ( shouldTurnBody )
				{
					// Store current yaw for rotation speed calculation
					var previousYaw = WorldRotation.Yaw();

					// Apply rotation
					WorldRotation = Rotation.Lerp( WorldRotation, desiredBodyRotation, BodyTurnSpeed * Time.Delta );

					// Calculate rotation speed (degrees per second)
					var newYaw = WorldRotation.Yaw();
					var yawDelta = Angles.NormalizeAngle( newYaw - previousYaw );
					_currentRotationSpeed = MathF.Abs( yawDelta ) / Time.Delta;
				}
				else
				{
					_currentRotationSpeed = 0f;
				}
			}
			else
			{
				_currentRotationSpeed = 0f;
			}
		}
		else
		{
			_currentRotationSpeed = 0f;
		}
	}

	TimeSince _timeSinceGatheredTargets;
	List<IActor> _potentialTargets = new();

	/// <summary>
	/// Updates the NPC's perception, look for targets every second that are nearby
	/// </summary>
	private void UpdatePerception()
	{
		_friends.Clear();
		_enemies.Clear();

		if ( _timeSinceGatheredTargets > 1f )
		{
			_potentialTargets = Scene.GetAll<IActor>()
				.Where( x => x.WorldPosition.Distance( WorldPosition ) <= DetectionRange )
				.Where( x => x != this )
				.ToList();

			_timeSinceGatheredTargets = 0;
		}

		foreach ( var target in _potentialTargets )
		{
			//
			// Targets could become invalid because we're fetching periodically
			//
			if ( !target.IsValid() ) continue;

			//
			// Hostiles: everyone is an enemy
			//
			if ( Relationship is Relationship.Hostile )
			{
				_enemies.Add( target );
				continue;
			}

			//
			// Revenge
			//
			if ( _attackers.Contains( target ) )
			{
				_enemies.Add( target );
				continue;
			}

			//
			// Friendlies and Neutrals: player is friend; hostile NPCs are enemies
			//
			if ( target is Player player )
			{
				if ( Relationship is Relationship.Friendly or Relationship.Neutral )
					_friends.Add( player );

				continue;
			}

			if ( target is Npc npc )
			{
				if ( npc.Relationship is Relationship.Hostile )
				{
					_enemies.Add( npc );
				}
				else if ( Relationship is Relationship.Neutral && npc.Relationship is Relationship.Friendly or Relationship.Neutral )
				{
					_friends.Add( npc );
				}
			}
		}
	}

	float _currentRotationSpeed;

	private void UpdateAnimation()
	{
		if ( !Renderer.IsValid() )
			return;

		var vel = NavMeshAgent?.Velocity ?? Vector3.Zero;

		var forward = WorldRotation.Forward.Dot( vel );
		var side = WorldRotation.Right.Dot( vel );

		Renderer.Set( "move_x", forward );
		Renderer.Set( "move_y", side );
		Renderer.Set( "move_speed", vel.Length );
		Renderer.Set( "move_rotationspeed", _currentRotationSpeed );
		Renderer.Set( "holdtype", _weapon.IsValid() ? (int)_weapon.HoldType : 0 );
	}
}
