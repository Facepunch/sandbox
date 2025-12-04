using Sandbox.Navigation;
using System.Text.Json.Serialization;
using System.Threading;

namespace Sandbox.AI;

public sealed partial class Npc
{
	/// <summary>
	/// Distance NPCs try to maintain from their target during combat
	/// </summary>
	[Property, Range( 128f, 1024f ), Group( "Skill" )] public float CombatRange { get; set; } = 512f;

	/// <summary>
	/// How often NPCs reposition during combat (seconds)
	/// </summary>
	[Property, Range( 1f, 10f ), Group( "Skill" )] public float RepositionInterval { get; set; } = 3f;

	/// <summary>
	/// Distance NPCs move when repositioning during combat
	/// </summary>
	[Property, Range( 64f, 512f ), Group( "Skill" )] public float RepositionDistance { get; set; } = 256f;

	/// <summary>
	/// NPC's aiming skill level (0.0 = terrible aim, 1.0 = perfect aim)
	/// </summary>
	[Property, Range( 0, 1 ), Step( 0.05f ), Group( "Skill" )] public float AimingSkill { get; set; } = 0.5f;

	/// <summary>
	/// How far away do we start shooting at a target -- this could probably be on the weapon
	/// </summary>
	[Property, Range( 256, 16834 ), Step( 1 ), Group( "Skill" )] private float AttackRange { get; set; } = 4096;

	List<IActor> _friends = new();
	List<IActor> _enemies = new();
	HashSet<IActor> _attackers = new(); // Remember who has attacked this NPC

	IActor _currentTarget;
	BaseCarryable _weapon;

	CancellationTokenSource _cts;

	[Property, ReadOnly, JsonIgnore, Group( "Data" )]
	State _currentState = State.Idle;

	/// <summary>
	/// Check if we have line of sight to a target
	/// </summary>
	private bool HasLineOfSight( IActor target )
	{
		if ( !target.IsValid() )
			return false;

		var startPos = EyeTransform.Position;
		var endPos = target.EyeTransform.Position;
		var trace = Scene.Trace.Ray( startPos, endPos )
			.WithoutTags( "trigger" )
			.Run();

		// If we hit nothing or hit the target, we have line of sight
		return !trace.Hit || trace.GameObject == target.GameObject;
	}

	private State DecideState()
	{
		var hp = Health / MaxHealth * 100f;

		// Simple scared level check - if too scared, flee from closest target
		if ( ScaredLevel >= ScaredFleeThreshold )
		{
			_currentTarget = FindClosest( _potentialTargets.Where( t => t.IsValid() ).ToList() );
			if ( _currentTarget is not null )
				return State.Flee;
		}

		if ( _enemies.Count > 0 )
		{
			_currentTarget = FindClosest( _enemies );
			if ( _currentTarget is not null )
			{
				var d = DistanceTo( _currentTarget );
				if ( d <= AttackRange ) return State.Attack;
				if ( d <= DetectionRange ) return State.Move;
			}
		}

		//
		// Follow behavior for friendly NPCs (scared NPCs stay closer)
		//
		if ( Relationship == Relationship.Friendly )
		{
			_currentTarget = FindClosest( _friends );
			if ( _currentTarget is not null && _currentTarget is Player )
			{
				var d = DistanceTo( _currentTarget );
				var scaredFollowDistance = FollowDistance * (1f - ScaredLevel / 200f); // Scared NPCs want to stay closer

				if ( d > scaredFollowDistance + FollowTolerance || d < scaredFollowDistance - FollowTolerance )
					return State.Follow;
			}
		}

		//
		// Keep distance behavior for neutral NPCs - scared NPCs keep more distance
		//
		if ( Relationship == Relationship.Neutral )
		{
			var closestPlayer = FindClosest( _friends.Where( f => f is Player ).ToList() );
			if ( closestPlayer is not null )
			{
				var d = DistanceTo( closestPlayer );
				var scaredKeepDistance = FollowDistance + (ScaredLevel * 2f); // More scared = keep more distance

				if ( d < scaredKeepDistance - FollowTolerance )
				{
					_currentTarget = closestPlayer;
					return State.KeepDistance;
				}
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
			case State.KeepDistance: _ = KeepDistanceLoop( t ); break;
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
			TimeSince lastReposition = 0;
			Vector3? currentCombatPosition = null;

			while ( !t.IsCancellationRequested )
			{
				if ( _currentTarget?.IsValid() != true )
					break;

				var distanceToTarget = DistanceTo( _currentTarget );
				if ( distanceToTarget > AttackRange )
					break;

				// Determine if we need to reposition
				var shouldReposition = false;

				// Reposition if we don't have a combat position yet
				if ( currentCombatPosition == null )
				{
					shouldReposition = true;
				}
				// Reposition periodically to stay mobile
				else if ( lastReposition > RepositionInterval )
				{
					shouldReposition = true;
				}
				// Reposition if target got too close (maintain combat distance)
				else if ( distanceToTarget < CombatRange * 0.7f )
				{
					shouldReposition = true;
				}
				// Reposition if target got too far (try to close distance)
				else if ( distanceToTarget > CombatRange * 1.5f )
				{
					shouldReposition = true;
				}

				if ( shouldReposition )
				{
					var newPosition = FindTacticalPosition( _currentTarget );
					if ( newPosition.HasValue )
					{
						currentCombatPosition = newPosition;
						NavMeshAgent.MoveTo( currentCombatPosition.Value );
						lastReposition = 0;
					}
				}
				else if ( currentCombatPosition.HasValue )
				{
					// Move to our combat position if we're not there yet
					var distanceToPosition = Vector3.DistanceBetween( WorldPosition, currentCombatPosition.Value );
					if ( distanceToPosition > 32f )
					{
						NavMeshAgent.MoveTo( currentCombatPosition.Value );
					}
				}

				// Attack if we have a weapon and are in range
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

	/// <summary>
	/// Finds a tactical position for combat - tries to maintain good distance and avoid being in the open
	/// </summary>
	private Vector3? FindTacticalPosition( IActor target )
	{
		if ( !target.IsValid() )
			return null;

		var targetPos = target.WorldPosition;
		var directionToTarget = (targetPos - WorldPosition).Normal;

		// Generate potential positions around the target at combat range
		var testPositions = new List<Vector3>();

		// Try different angles around the target
		var angles = new[] { -90f, -45f, 0f, 45f, 90f, 135f, 180f, -135f };

		foreach ( var angle in angles )
		{
			var rotation = Rotation.FromYaw( angle );
			var offsetDirection = rotation * directionToTarget;

			// Try different distances
			var distances = new[] { CombatRange, CombatRange * 0.8f, CombatRange * 1.2f };

			foreach ( var distance in distances )
			{
				var testPos = targetPos - (offsetDirection * distance);
				testPositions.Add( testPos );
			}
		}

		// Also add some random positions for unpredictability
		for ( int i = 0; i < 5; i++ )
		{
			var randomAngle = Game.Random.Float( 0f, 360f );
			var randomDistance = Game.Random.Float( CombatRange * 0.7f, CombatRange * 1.3f );
			var randomRotation = Rotation.FromYaw( randomAngle );
			var randomDir = randomRotation * Vector3.Forward;
			var randomPos = targetPos - (randomDir * randomDistance);
			testPositions.Add( randomPos );
		}

		// Evaluate positions and pick the best one
		Vector3? bestPosition = null;
		float bestScore = float.MinValue;

		foreach ( var testPos in testPositions )
		{
			// Check if position is valid on navmesh
			if ( Scene.NavMesh.GetClosestPoint( testPos ) is not Vector3 navPos ||
				 Vector3.DistanceBetween( testPos, navPos ) > 64f )
			{
				continue;
			}

			var score = EvaluateTacticalPosition( navPos, target );
			if ( score > bestScore )
			{
				bestScore = score;
				bestPosition = navPos;
			}
		}

		return bestPosition;
	}

	/// <summary>
	/// Evaluates how good a position is for combat
	/// </summary>
	private float EvaluateTacticalPosition( Vector3 position, IActor target )
	{
		float score = 0f;

		var distanceToTarget = Vector3.DistanceBetween( position, target.WorldPosition );
		var distanceToMe = Vector3.DistanceBetween( position, WorldPosition );

		// Prefer positions at ideal combat range
		var idealDistance = CombatRange;
		var distanceScore = 100f - MathF.Abs( distanceToTarget - idealDistance );
		score += distanceScore;

		// Prefer positions that aren't too far from current position (don't want to run across the map)
		if ( distanceToMe > RepositionDistance * 2f )
		{
			score -= 50f;
		}

		// Check line of sight to target
		var trace = Scene.Trace.Ray( position + Vector3.Up * 64f, target.EyeTransform.Position )
			.WithoutTags( "trigger" )
			.Run();

		if ( !trace.Hit || trace.GameObject == target.GameObject )
		{
			score += 30f; // Bonus for clear line of sight
		}
		else
		{
			score -= 20f; // Penalty for blocked line of sight
		}

		// Small random factor for unpredictability
		score += Game.Random.Float( -10f, 10f );

		return score;
	}

	private async Task FleeLoop( CancellationToken t )
	{
		try
		{
			while ( !t.IsCancellationRequested )
			{
				// Simple: flee from the current target (whoever made us scared)
				if ( !_currentTarget.IsValid() )
					break;

				var distanceToTarget = DistanceTo( _currentTarget );
				var hasLOS = HasLineOfSight( _currentTarget );

				// Scared NPCs need to get further away to feel safe
				var scaredDistanceMultiplier = 1f + (ScaredLevel / 100f);
				var safeDistance = FleeRange * 0.9f * scaredDistanceMultiplier;
				var moderateSafeDistance = FleeRange * 0.6f * scaredDistanceMultiplier;

				// Check if we should stop fleeing
				bool shouldStopFleeing = false;

				// Stop fleeing if we're far enough away (distance affected by scared level)
				if ( distanceToTarget > safeDistance )
				{
					shouldStopFleeing = true;
				}
				// Or if we're a reasonable distance away AND don't have line of sight
				else if ( distanceToTarget > moderateSafeDistance && !hasLOS )
				{
					shouldStopFleeing = true;
				}

				if ( shouldStopFleeing )
				{
					// Clear attackers list after successfully fleeing
					_attackers.Clear();
					// Reduce scared level when successfully escaping
					AddScare( -10f );
					break; // Exit flee state, will re-evaluate in DecideState()
				}

				// Calculate base direction away from target
				var baseDir = (WorldPosition - _currentTarget.WorldPosition).Normal;

				// Add some randomness to avoid straight-line fleeing
				// Generate angles between -60 and +60 degrees from the direct flee direction
				var fleeAngles = new[]
				{
					0f,     // Straight away (fallback)
					-30f,   // Left side
					30f,    // Right side
					-60f,   // Far left
					60f,    // Far right
					-45f,   // Left-diagonal
					45f     // Right-diagonal
				};

				Vector3? fleePosition = null;
				var distances = new[] { FleeRange, FleeRange * 0.75f, FleeRange * 0.5f };

				// Try different angles and distances to find the best flee position
				foreach ( var distance in distances )
				{
					foreach ( var angle in fleeAngles )
					{
						// Rotate the base direction by the angle
						var rotation = Rotation.FromYaw( angle );
						var rotatedDir = rotation * baseDir;
						var testPos = WorldPosition + rotatedDir * distance;

						// Check if this position is valid on the navmesh
						if ( Scene.NavMesh.GetClosestPoint( testPos ) is Vector3 navPos &&
							 Vector3.DistanceBetween( testPos, navPos ) < 128f )
						{
							fleePosition = navPos;
							break;
						}
					}

					if ( fleePosition.HasValue )
						break;
				}

				// Fallback: if no good flee position found, just move directly away for a shorter distance
				if ( fleePosition is null )
				{
					fleePosition = WorldPosition + baseDir * 256f;
				}

				NavMeshAgent.MoveTo( fleePosition.Value );

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
				var scaredFollowDistance = FollowDistance * (1f - ScaredLevel / 200f); // Scared NPCs stay closer

				if ( d >= scaredFollowDistance - FollowTolerance && d <= scaredFollowDistance + FollowTolerance )
					break;

				var dir = (_currentTarget.WorldPosition - WorldPosition).Normal;
				NavMeshAgent.MoveTo( _currentTarget.WorldPosition - dir * scaredFollowDistance );

				await Task.Delay( 150, t );
			}
		}
		catch { }
	}

	private async Task KeepDistanceLoop( CancellationToken t )
	{
		try
		{
			while ( !t.IsCancellationRequested )
			{
				if ( !_currentTarget.IsValid() )
					break;

				var d = DistanceTo( _currentTarget );
				var scaredKeepDistance = FollowDistance + (ScaredLevel * 2f); // More scared = keep more distance

				if ( d >= scaredKeepDistance - FollowTolerance )
					break;

				// Move away from the player to maintain desired distance
				var dir = (WorldPosition - _currentTarget.WorldPosition).Normal;
				NavMeshAgent.MoveTo( WorldPosition + dir * (scaredKeepDistance - d + FollowTolerance) );

				await Task.Delay( 150, t );
			}
		}
		catch { }
	}
}
