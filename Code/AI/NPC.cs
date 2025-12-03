using Sandbox;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Sandbox.Component;

/// <summary>
/// The goal of this class is to provide a configurable easy NPC.
/// </summary>
public sealed class NPC : Component, IDamageable, IActor
{
	[RequireComponent] NavMeshAgent NavMeshAgent { get; set; }

	/// <summary>
	/// The body of the npc
	/// </summary>
	[Property] public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// Where are their eyes?
	/// </summary>
	[Property] public GameObject EyeSource { get; set; }

	/// <summary>
	/// Optionally spawn a weapon in the NPC's hands that they can use
	/// </summary>
	[Property] public GameObject WeaponPrefab { get; set; }

	/// <summary>
	/// The NPC's relationship to other NPCs and players
	/// </summary>
	[Property] public Relationship CurrentRelationship { get; set; } = Relationship.Neutral;

	/// <summary>
	/// How healthy is this npc
	/// </summary>
	[Property] public float Health { get; set; } = 100f;

	/// <summary>
	/// The max hp, used for thresholds for fleeing right now
	/// </summary>
	[Property] public float MaxHealth { get; set; } = 100f;

	/// <summary>
	/// How far away do we search for targets
	/// </summary>
	[Property] public float DetectionRange { get; set; } = 4096f;

	/// <summary>
	/// The health threshold for a npc running away from its target  
	/// </summary>
	[Property] public float FleeThreshold { get; set; } = 25f;

	/// <summary>
	/// How far does the NPC flee
	/// </summary>
	[Property] public float FleeRange { get; set; } = 4096f;

	/// <summary>
	/// Constraint for the look pitch
	/// </summary>
	[Property] public RangedFloat LookPitch = new( -45f, 45f );

	/// <summary>
	/// Constraint for the look yaw
	/// </summary>
	[Property] public RangedFloat LookYaw = new( -60f, 60f );

	/// <summary>
	/// How fast the body turns to follow the eye target
	/// </summary>
	[Property] public float BodyTurnSpeed { get; set; } = 6f;

	/// <summary>
	/// Minimum angle difference (in degrees) before the body will turn to follow the target
	/// </summary>
	[Property] public float FootShuffleThreshold { get; set; } = 45f;

	/// <summary>
	/// NPC's aiming skill level (0.0 = terrible aim, 1.0 = perfect aim)
	/// </summary>
	[Property, Range( 0, 1 ), Step( 0.05f )] public float AimingSkill { get; set; } = 0.5f;

	/// <summary>
	/// How far away do we start shooting at a target -- this could probably be on the weapon
	/// </summary>
	[Property] private float AttackRange { get; set; } = 4096;

	/// <summary>
	/// If we're following a friendly, what's the desired distance away from them? The npc will try to abide by this
	/// </summary>
	[Property] public float FollowDistance { get; set; } = 300f;

	/// <summary>
	/// Distance tolerance so they don't just go back and forth 
	/// </summary>
	[Property] public float FollowTolerance { get; set; } = 50f;

	private readonly List<IActor> _friends = new();
	private readonly List<IActor> _enemies = new();

	private IActor _currentTarget;
	private BaseCarryable _weapon;

	private CancellationTokenSource _cts;

	/// <summary>
	/// Current eye target position in world space. Null means no target.
	/// </summary>
	private Vector3? _eyeTarget;

	public enum Relationship
	{
		Neutral,
		Friendly,
		Hostile
	}

	public enum State
	{
		Idle,
		Move,
		Attack,
		Flee,
		Follow
	}

	private State _currentState = State.Idle;

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

		Animate();
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

	/// <summary>
	/// Calculates aim offset based on skill level and distance to target
	/// </summary>
	/// <returns>Modified aim position with skill-based inaccuracy</returns>
	private Vector3 CalculateAimVector( Vector3 targetPosition, float distance )
	{
		// Perfect aim (skill = 1.0) returns exact target position
		if ( AimingSkill >= 1f )
			return targetPosition;

		// Calculate maximum spread based on inverse skill level
		// Lower skill = higher spread, distance also increases spread
		var maxSpread = (1f - AimingSkill) * 100f; 
		var distanceMultiplier = distance / 1000f;
		var totalSpread = maxSpread * (1f + distanceMultiplier);

		// Add random offset in a circle around the target
		var randomAngle = Game.Random.Float( 0f, 360f );
		var randomDistance = Game.Random.Float( 0f, totalSpread );

		var offsetX = MathF.Cos( MathF.PI * randomAngle / 180f ) * randomDistance;
		var offsetY = MathF.Sin( MathF.PI * randomAngle / 180f ) * randomDistance;

		return targetPosition + new Vector3( offsetX, offsetY, 0f );
	}

	private void UpdateEyeTarget()
	{
		Vector3? newTarget = null;

		if ( _currentState == State.Idle )
		{
			var friend = FindClosest( _friends );
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
			return;
		}

		var eyePosition = EyeTransform.Position;
		var targetPosition = _eyeTarget.Value;
		targetPosition = targetPosition.WithZ( eyePosition.z );

		var lookDirection = (targetPosition - eyePosition).Normal;

		if ( lookDirection.IsNearlyZero() )
			return;

		// Always try to look at the target with head and body
		var desiredBodyDirection = lookDirection.WithZ( 0 ).Normal;
		var headLookDirection = lookDirection;

		if ( !desiredBodyDirection.IsNearlyZero() )
		{
			var desiredBodyRotation = Rotation.LookAt( desiredBodyDirection, Vector3.Up );
			var currentYaw = WorldRotation.Yaw();
			var desiredYaw = desiredBodyRotation.Yaw();
			var yawDifference = Angles.NormalizeAngle( desiredYaw - currentYaw );

			// Only rotate if the difference is significant enough
			if ( MathF.Abs( yawDifference ) > 5f )
			{
				WorldRotation = Rotation.Lerp( WorldRotation, desiredBodyRotation, BodyTurnSpeed * Time.Delta );
			}
		}

		// Update head and eye look using Vector3 parameters
		if ( Renderer.IsValid() )
		{
			// Project the head look direction forward by 1024 units to prevent steep upward angles for close objects
			var localTargetPosition = WorldTransform.PointToLocal( eyePosition + headLookDirection * 1024f );

			Renderer.Set( "aim_head", localTargetPosition.Normal );
			Renderer.Set( "aim_eyes", localTargetPosition.Normal );
		}

		EyeSource.WorldRotation = Rotation.LookAt( headLookDirection );
	}

	private State DecideState()
	{
		var hp = Health / MaxHealth * 100f;

		if ( hp <= FleeThreshold && _enemies.Count > 0 )
			return State.Flee;

		_currentTarget = FindClosest( _enemies );
		if ( _currentTarget is not null )
		{
			var d = DistanceTo( _currentTarget );
			if ( d <= AttackRange ) return State.Attack;
			if ( d <= DetectionRange ) return State.Move;
		}

		//
		// Only follow players
		//
		if ( CurrentRelationship == Relationship.Friendly )
		{
			_currentTarget = FindClosest( _friends );
			if ( _currentTarget is not null && _currentTarget is Player )
			{
				var d = DistanceTo( _currentTarget );
				if ( d > FollowDistance + FollowTolerance || d < FollowDistance - FollowTolerance )
					return State.Follow;
			}
		}

		_currentTarget = null;
		return State.Idle;
	}

	private void UpdateState()
	{
		var newState = DecideState();
		if ( newState == _currentState )
			return;

		_currentState = newState;
		CancelTasks();
		_cts = new CancellationTokenSource();
		var t = _cts.Token;

		switch ( newState )
		{
			case State.Idle: _ = IdleLoop( t ); break;
			case State.Move: _ = MoveLoop( t ); break;
			case State.Attack: _ = AttackLoop( t ); break;
			case State.Flee: _ = FleeLoop( t ); break;
			case State.Follow: _ = FollowLoop( t ); break;
		}
	}

	private void CancelTasks()
	{
		if ( _cts is null ) return;
		_cts.Cancel();
		_cts.Dispose();
		_cts = null;
	}

	private async Task IdleLoop( CancellationToken t )
	{
		try
		{
			NavMeshAgent.MoveTo( WorldPosition );
			while ( !t.IsCancellationRequested )
				await Task.Delay( 200, t );
		}
		catch { }
	}

	private async Task MoveLoop( CancellationToken t )
	{
		try
		{
			while ( !t.IsCancellationRequested )
			{
				if ( _currentTarget?.IsValid() != true )
					break;

				var pos = _currentTarget.WorldPosition;
				NavMeshAgent.MoveTo( pos );

				var d = DistanceTo( _currentTarget );
				if ( d <= AttackRange || d > DetectionRange )
					break;

				await Task.Delay( 50, t );
			}
		}
		catch { }
	}


	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void TriggerAnimation( string animation )
	{
		Renderer?.Set( animation, true );
	}

	private async Task AttackLoop( CancellationToken t )
	{
		try
		{
			while ( !t.IsCancellationRequested )
			{
				if ( _currentTarget?.IsValid() != true )
					break;
				if ( DistanceTo( _currentTarget ) > AttackRange )
					break;

				NavMeshAgent.MoveTo( WorldPosition );

				if ( _weapon is BaseWeapon weapon )
				{
					if ( weapon.CanPrimaryAttack() )
					{
						TriggerAnimation( "b_attack" );
						weapon.PrimaryAttack();
					}

					if ( !weapon.HasAmmo() )
					{
						TriggerAnimation( "b_reload" );
						await weapon.ReloadAsync( _cts.Token );
					}
				}

				await Task.Delay( 100, t );
			}
		}
		catch { }
	}

	private async Task FleeLoop( CancellationToken t )
	{
		try
		{
			while ( !t.IsCancellationRequested )
			{
				var enemy = FindClosest( _enemies );
				if ( enemy is null ) break;

				var dir = (WorldPosition - enemy.WorldPosition).Normal;
				NavMeshAgent.MoveTo( WorldPosition + dir * FleeRange );

				await Task.Delay( 150, t );
			}
		}
		catch { }
	}

	private async Task FollowLoop( CancellationToken t )
	{
		try
		{
			while ( !t.IsCancellationRequested )
			{
				// Only follow players
				if ( !_currentTarget.IsValid() || _currentTarget is not Player )
					break;

				var d = DistanceTo( _currentTarget );
				if ( d >= FollowDistance - FollowTolerance && d <= FollowDistance + FollowTolerance )
					break;

				var dir = (_currentTarget.WorldPosition - WorldPosition).Normal;
				NavMeshAgent.MoveTo( _currentTarget.WorldPosition - dir * FollowDistance );

				await Task.Delay( 150, t );
			}
		}
		catch { }
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
			if ( CurrentRelationship is Relationship.Hostile )
			{
				_enemies.Add( target );
				continue;
			}

			//
			// Friendlies: player is friend; hostile NPCs are enemies; ignore non-hostile NPCs for follow
			//
			if ( target is Player player )
			{
				if ( CurrentRelationship is Relationship.Friendly )
					_friends.Add( player );

				continue;
			}

			if ( target is NPC npc )
			{
				if ( npc.CurrentRelationship is Relationship.Hostile )
				{
					_enemies.Add( npc );
				}
			}
		}
	}

	private IActor FindClosest( List<IActor> list )
	{
		return list
			.Where( v => v.IsValid() )
			.OrderBy( DistanceTo )
			.FirstOrDefault();
	}

	private float DistanceTo( IActor actor ) =>
		Vector3.DistanceBetween( WorldPosition, actor.WorldPosition );

	/// <summary>
	/// Gets the eye position of an actor for targeting
	/// </summary>
	private Vector3 GetEye( IActor actor )
	{
		return actor.EyeTransform.Position;
	}

	private void Animate()
	{
		if ( !Renderer.IsValid() )
			return;

		var vel = NavMeshAgent?.Velocity ?? Vector3.Zero;

		var forward = WorldRotation.Forward.Dot( vel );
		var side = WorldRotation.Right.Dot( vel );

		Renderer.Set( "move_x", forward );
		Renderer.Set( "move_y", side );
		Renderer.Set( "move_speed", vel.Length );
		Renderer.Set( "holdtype", _weapon.IsValid() ? (int)_weapon.HoldType : 0 );
	}

	public void OnDamage( in DamageInfo info )
	{
		if ( Health <= 0 )
			return;

		Health -= info.Damage;

		if ( Health >= 1 )
			return;

		CancelTasks();
		CreateRagdoll();
		GameObject.Destroy();
	}

	/// <summary>
	/// Create a ragdoll gameobject version of our render body.
	/// </summary>
	public GameObject CreateRagdoll( string name = "Ragdoll" )
	{
		var go = new GameObject( true, name );
		go.Tags.Add( "ragdoll" );
		go.WorldTransform = WorldTransform;

		var originalBody = Renderer.Components.Get<SkinnedModelRenderer>();

		if ( !originalBody.IsValid() )
			return go;

		var mainBody = go.Components.Create<SkinnedModelRenderer>();
		mainBody.CopyFrom( originalBody );
		mainBody.UseAnimGraph = false;

		// copy the clothes
		foreach ( var clothing in originalBody.GameObject.Children.SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
		{
			if ( !clothing.IsValid() ) continue;

			var newClothing = new GameObject( true, clothing.GameObject.Name );
			newClothing.Parent = go;

			var item = newClothing.Components.Create<SkinnedModelRenderer>();
			item.CopyFrom( clothing );
			item.BoneMergeTarget = mainBody;
		}

		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;
		physics.CopyBonesFrom( originalBody, true );

		return go;
	}
}
