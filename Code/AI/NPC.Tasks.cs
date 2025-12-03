using System.Threading;

namespace Sandbox.AI;

public sealed partial class Npc
{
	List<IActor> _friends = new();
	List<IActor> _enemies = new();
	HashSet<IActor> _attackers = new(); // Remember who has attacked this NPC

	IActor _currentTarget;
	BaseCarryable _weapon;

	CancellationTokenSource _cts;
	State _currentState = State.Idle;

	private State DecideState()
	{
		var hp = Health / MaxHealth * 100f;

		// For neutral NPCs without weapons, flee from attackers instead of fighting
		if ( Relationship == Relationship.Neutral && !HasWeapon() && _attackers.Count > 0 )
		{
			_currentTarget = FindClosest( _attackers.Where( a => a.IsValid() && DistanceTo( a ) <= DetectionRange ).ToList() );
			if ( _currentTarget is not null )
				return State.Flee;
		}

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
		// Follow behavior for friendly NPCs
		//
		if ( Relationship == Relationship.Friendly )
		{
			_currentTarget = FindClosest( _friends );
			if ( _currentTarget is not null && _currentTarget is Player )
			{
				var d = DistanceTo( _currentTarget );
				if ( d > FollowDistance + FollowTolerance || d < FollowDistance - FollowTolerance )
					return State.Follow;
			}
		}

		//
		// Keep distance behavior for neutral NPCs - avoid getting too close to players
		//
		if ( Relationship == Relationship.Neutral )
		{
			var closestPlayer = FindClosest( _friends.Where( f => f is Player ).ToList() );
			if ( closestPlayer is not null )
			{
				var d = DistanceTo( closestPlayer );
				if ( d < FollowDistance - FollowTolerance )
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
				// For neutral NPCs, prioritize fleeing from attackers if they have no weapon
				IActor enemy = null;
				if ( Relationship == Relationship.Neutral && !HasWeapon() && _attackers.Count > 0 )
				{
					enemy = FindClosest( _attackers.Where( a => a.IsValid() && DistanceTo( a ) <= DetectionRange ).ToList() );
				}

				if ( enemy is null )
				{
					enemy = FindClosest( _enemies );
				}

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

	private async Task KeepDistanceLoop( CancellationToken t )
	{
		try
		{
			while ( !t.IsCancellationRequested )
			{
				if ( !_currentTarget.IsValid() )
					break;

				var d = DistanceTo( _currentTarget );
				if ( d >= FollowDistance - FollowTolerance )
					break;

				// Move away from the player to maintain desired distance
				var dir = (WorldPosition - _currentTarget.WorldPosition).Normal;
				NavMeshAgent.MoveTo( WorldPosition + dir * (FollowDistance - d + FollowTolerance) );

				await Task.Delay( 150, t );
			}
		}
		catch { }
	}
}
