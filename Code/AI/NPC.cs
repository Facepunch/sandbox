using System.Text.Json.Serialization;

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
	/// How far does the NPC flee
	/// </summary>
	[Property, Group( "Skill" )] public float FleeRange { get; set; } = 4096f;

	/// <summary>
	/// Current scared level (0-100) - affects likelihood to flee
	/// </summary>
	[Property, ReadOnly, JsonIgnore, Range( 0f, 100f ), Group( "Data" )] public float ScaredLevel { get; private set; }

	/// <summary>
	/// How quickly scared level decreases over time (per second)
	/// </summary>
	[Property, Range( 1f, 20f ), Group( "Skill" )] public float ScaredDecayRate { get; set; } = 5f;

	/// <summary>
	/// Scared level at which NPC will start considering fleeing
	/// </summary>
	[Property, Range( 20f, 80f ), Group( "Skill" )] public float ScaredFleeThreshold { get; set; } = 50f;

	/// <summary>
	/// How much damage increases scared level (per point of damage)
	/// </summary>
	[Property, Range( 0.5f, 5f ), Group( "Skill" )] public float DamageScareMultiplier { get; set; } = 2f;

	/// <summary>
	/// Distance at which neutral NPCs start feeling uncomfortable with players
	/// </summary>
	[Property, Range( 64f, 256f ), Group( "Skill" )] public float PersonalSpaceDistance { get; set; } = 128f;

	/// <summary>
	/// How much scared level increases per second when players are too close
	/// </summary>
	[Property, Range( 0f, 50f ), Group( "Skill" ), ShowIf( nameof( Relationship ), AI.Relationship.Neutral )] 
	public float ProximityScareRate { get; set; } = 3f;

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
	[Property, Range( 2f, 15f ), Group( "Body" )] public float BodyTurnSpeed { get; set; } = 5f;

	/// <summary>
	/// If we're following a friendly, what's the desired distance away from them? The npc will try to abide by this
	/// </summary>
	[Property, Range( 64, 512f )] public float FollowDistance { get; set; } = 300f;

	/// <summary>
	/// Distance tolerance so they don't just go back and forth 
	/// </summary>
	[Property, Range( 4f, 64f )] public float FollowTolerance { get; set; } = 50f;

	Vector3? _eyeTarget;
	bool _isCompletingTurn;
	Rotation _targetBodyRotation;

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
			UpdateScaredLevel();
			UpdatePerception();
			UpdateEyeTarget();
			UpdateEyeSystem();
			UpdateState();
		}

		UpdateAnimation();
	}

	/// <summary>
	/// Updates the scared level, gradually decreasing it over time and checking for proximity stress
	/// </summary>
	private void UpdateScaredLevel()
	{
		// Check for proximity stress (neutral NPCs only)
		if ( Relationship == Relationship.Neutral )
		{
			var closestPlayer = _potentialTargets?.OfType<Player>()
				.Where( p => p.IsValid() )
				.OrderBy( p => DistanceTo( p ) )
				.FirstOrDefault();

			if ( closestPlayer.IsValid() )
			{
				var distance = DistanceTo( closestPlayer );
				if ( distance < PersonalSpaceDistance )
				{
					// Calculate proximity stress - closer = more stress
					var proximityFactor = 1f - (distance / PersonalSpaceDistance);
					var scareIncrease = ProximityScareRate * proximityFactor * Time.Delta;
					AddScare( scareIncrease );
				}
			}
		}

		// Natural scared level decay over time
		if ( ScaredLevel > 0f )
		{
			ScaredLevel = MathF.Max( 0f, ScaredLevel - ScaredDecayRate * Time.Delta );
		}
	}

	/// <summary>
	/// Increase the NPC's scared level
	/// </summary>
	/// <param name="amount">Amount to increase scared level by</param>
	public void AddScare( float amount )
	{
		ScaredLevel = MathF.Min( 100f, ScaredLevel + amount );
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
		_eyeTarget = worldPosition;
	}

	private void UpdateEyeTarget()
	{
		Vector3? newTarget = null;

		if ( _currentState == State.Idle )
		{
			// First, prioritize players within idle look range
			var closestPlayer = _friends
				.OfType<Player>()
				.Where( p => p.IsValid() && DistanceTo( p ) <= IdleLookRange )
				.OrderBy( p => DistanceTo( p ) )
				.FirstOrDefault();

			if ( closestPlayer != null )
			{
				newTarget = GetEye( closestPlayer );
			}
			else
			{
				// Only look at other NPCs if no players are nearby
				var closestNpc = _friends
					.OfType<Npc>()
					.Where( npc => npc.IsValid() && DistanceTo( npc ) <= IdleLookRange )
					.OrderBy( npc => DistanceTo( npc ) )
					.FirstOrDefault();

				if ( closestNpc != null )
					newTarget = GetEye( closestNpc );
			}
		}
		else if ( _currentState == State.Flee )
		{
			// When fleeing, don't look at the target - look forward in movement direction or straight ahead
			var moveDirection = NavMeshAgent?.Velocity.Normal ?? Vector3.Zero;
			if ( !moveDirection.IsNearlyZero() )
			{
				// Look in the direction we're moving
				newTarget = EyeTransform.Position + moveDirection * 1024f;
			}
			else
			{
				// Look straight ahead if not moving
				newTarget = EyeTransform.Position + WorldRotation.Forward * 1024f;
			}
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

	/// <summary>
	/// Determines the desired body rotation and rotation threshold based on current state and movement
	/// </summary>
	private (Rotation rotation, float threshold) GetBodyRotationBehavior( Vector3 lookDirection, bool isMoving )
	{
		var defaultRotation = Rotation.LookAt( lookDirection.WithZ( 0 ).Normal, Vector3.Up );

		return _currentState switch
		{
			// Always face attack target when shooting
			State.Attack => (defaultRotation, 0f),

			// When moving, always face target (low threshold)
			State.Flee when isMoving => (defaultRotation, 5f),
			State.Follow when isMoving => (defaultRotation, 5f),
			State.Idle when isMoving => (defaultRotation, 5f),

			_ => (defaultRotation, 45f),
		};
	}

	private void UpdateEyeSystem()
	{
		if ( _eyeTarget is null )
		{
			_currentRotationSpeed = 0f;
			return;
		}

		var eyePosition = EyeTransform.Position;
		var targetPosition = _eyeTarget.Value.WithZ( eyePosition.z );
		var lookDirection = (targetPosition - eyePosition).Normal;

		if ( lookDirection.IsNearlyZero() )
		{
			_currentRotationSpeed = 0f;
			return;
		}

		// Update head and eye look
		UpdateHeadAndEyeLook( lookDirection );

		// Handle body rotation - consider movement state
		var isMoving = NavMeshAgent?.Velocity.Length > 10f;
		var (desiredRotation, threshold) = GetBodyRotationBehavior( lookDirection, isMoving );
		UpdateBodyRotation( desiredRotation, threshold );
	}

	/// <summary>
	/// Updates the head and eye look directions
	/// </summary>
	private void UpdateHeadAndEyeLook( Vector3 lookDirection )
	{
		if ( Renderer.IsValid() )
		{
			// Project the head look direction forward by 1024 units to prevent steep upward angles for close objects
			var eyePosition = EyeTransform.Position;
			var localTargetPosition = WorldTransform.PointToLocal( eyePosition + lookDirection * 1024f );

			Renderer.Set( "aim_head", localTargetPosition.Normal );
			Renderer.Set( "aim_eyes", localTargetPosition.Normal );
		}

		EyeSource.WorldRotation = Rotation.LookAt( lookDirection );
	}

	/// <summary>
	/// Handles body rotation logic with turn completion
	/// </summary>
	private void UpdateBodyRotation( Rotation desiredRotation, float threshold )
	{
		var currentYaw = WorldRotation.Yaw();
		var desiredYaw = desiredRotation.Yaw();
		var yawDifference = Angles.NormalizeAngle( desiredYaw - currentYaw );

		bool shouldRotateBody = false;

		if ( _isCompletingTurn )
		{
			var targetYaw = _targetBodyRotation.Yaw();
			var currentTargetDifference = Angles.NormalizeAngle( targetYaw - currentYaw );

			if ( MathF.Abs( currentTargetDifference ) > 5f )
			{
				shouldRotateBody = true;
				desiredRotation = _targetBodyRotation;
			}
			else
			{
				_isCompletingTurn = false;
			}
		}
		else if ( MathF.Abs( yawDifference ) > threshold )
		{
			_isCompletingTurn = true;
			_targetBodyRotation = desiredRotation;
			shouldRotateBody = true;
		}

		if ( shouldRotateBody )
		{
			var previousYaw = WorldRotation.Yaw();
			WorldRotation = Rotation.Lerp( WorldRotation, desiredRotation, BodyTurnSpeed * Time.Delta );

			var newYaw = WorldRotation.Yaw();
			var yawDelta = Angles.NormalizeAngle( newYaw - previousYaw );
			_currentRotationSpeed = MathF.Abs( yawDelta ) / Time.Delta;
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
