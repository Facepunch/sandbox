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

	//
	// todo: Delete me and use the weapon's shit instead
	//
	[Property] private float AttackDamage { get; set; } = 25f;
	[Property] private float AttackRate { get; set; } = 1f;

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
			UpdateLook();
			UpdateBodyTurn();
			UpdateState();
		}

		Animate();
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

				TriggerAttack();

				if ( _weapon.IsValid() && _weapon is BaseWeapon weapon && weapon.CanShoot() )
					PrimaryAttack( _currentTarget );

				await Task.Delay( (int)(1000f / AttackRate), t );
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
				if ( _currentTarget.IsValid() != true || _currentTarget is not Player )
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

	private void UpdateBodyTurn()
	{
		if ( _currentState == State.Idle && _friends.Count == 0 )
			return;

		Vector3? look = null;

		if ( _currentTarget.IsValid() )
			look = GetEye( _currentTarget );
		else
			look = FindClosest( _friends ).IsValid() ? GetEye( FindClosest( _friends ) ) : null;

		if ( look is null )
			return;

		var dir = (look.Value - GetEye()).WithZ( 0 ).Normal;
		if ( dir.IsNearlyZero() ) return;

		var desired = Rotation.LookAt( dir, Vector3.Up );
		WorldRotation = Rotation.Lerp( WorldRotation, desired, 6f * Time.Delta );
	}

	private void UpdateLook()
	{
		Vector3? look = null;

		if ( _currentState == State.Idle )
		{
			var friend = FindClosest( _friends );
			if ( friend.IsValid() )
				look = GetEye( friend );
		}
		else if ( _currentTarget.IsValid() )
		{
			look = GetEye( _currentTarget );
		}

		if ( look is null )
		{
			Renderer.Set( "aim_body_pitch", 0f );
			Renderer.Set( "aim_body_yaw", 0f );
			return;
		}

		var dir = (look.Value - GetEye()).Normal;
		var local = WorldRotation.Inverse * Rotation.LookAt( dir );
		var ang = local.Angles();

		Renderer.Set( "aim_body_pitch", ang.pitch.Clamp( LookPitch.Min, LookPitch.Max ) );
		Renderer.Set( "aim_body_yaw", ang.yaw.Clamp( LookYaw.Min, LookYaw.Max ) );
	}

	private Vector3 GetEye( IActor actor = null )
	{
		if ( !actor.IsValid() )
			return EyeSource?.WorldPosition ?? (WorldPosition + Vector3.Up * 64f);

		//
		// Bit shit, might need a common method here
		//

		if ( actor is Player p )
			return p.EyeTransform.Position;

		if ( actor is NPC npc )
			return npc.GetEye();

		return actor.WorldPosition + Vector3.Up * 64f;
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

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void TriggerAttack()
	{
		Renderer?.Set( "b_attack", true );
	}

	private void PrimaryAttack( IActor target )
	{
		var muzzle = _weapon.MuzzleTransform.WorldPosition;
		var dir = (GetEye( target ) - muzzle).Normal;

		var tr = Scene.Trace
			.Ray( new Ray( muzzle, dir ), AttackRange * 2f )
			.IgnoreGameObjectHierarchy( GameObject )
			.UseHitboxes()
			.Run();

		if ( target.GetComponentInParent<IDamageable>() is not IDamageable dmg )
			return;

		var info = new DamageInfo( AttackDamage, GameObject, _weapon.IsValid() ? _weapon.GameObject : GameObject )
		{
			Position = tr.HitPosition,
			Origin = WorldPosition
		};

		dmg.OnDamage( info );
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
