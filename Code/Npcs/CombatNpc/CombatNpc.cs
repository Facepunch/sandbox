using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// A combat NPC that searches for players, advances on them, fires in bursts, and repositions.
///
/// </summary>
public class CombatNpc : Npc, Component.IDamageable
{
	[Property, ClientEditable, Range( 1, 500 ), Sync]
	public float Health { get; set; } = 100f;

	/// <summary>
	/// The weapon this NPC uses to attack.
	/// </summary>
	[Property]
	public BaseWeapon Weapon { get; set; }

	[Property, Group( "Balance" )]
	public float AttackRange { get; set; } = 300f;

	[Property, Group( "Balance" )]
	public float EngageSpeed { get; set; } = 180f;

	/// <summary>
	/// How long after losing sight of a player to keep searching their last known position.
	/// </summary>
	[Property, Group( "Balance" )]
	public float SearchTimeout { get; set; } = 8f;

	[Property, Group( "Balance" )]
	public float PatrolRadius { get; set; } = 400f;

	[Property, Group( "Balance" )]
	public float BurstDuration { get; set; } = 1.5f;

	[Property, Group( "Balance" )]
	public float BurstPause { get; set; } = 0.8f;

	private Vector3? _lastKnownPosition;
	private TimeSince _timeSinceLastSeen;

	protected override void OnStart()
	{
		base.OnStart();

		if ( Weapon.IsValid() && Renderer.IsValid() )
		{
			Weapon.CreateWorldModel( Renderer );
			Animation.SetHoldType( Weapon.HoldType );
		}
	}

	public override ScheduleBase GetSchedule()
	{
		var visible = Senses.GetNearestVisible();

		if ( visible.IsValid() )
		{
			_lastKnownPosition = visible.WorldPosition;
			_timeSinceLastSeen = 0;

			var engage = GetSchedule<CombatEngageSchedule>();
			engage.Target = visible;
			engage.Weapon = Weapon;
			engage.AttackRange = AttackRange;
			engage.EngageSpeed = EngageSpeed;
			engage.BurstDuration = BurstDuration;
			engage.BurstPause = BurstPause;
			return engage;
		}

		// Player not visible — search last known position if recent enough
		if ( _lastKnownPosition.HasValue && _timeSinceLastSeen < SearchTimeout )
		{
			var search = GetSchedule<ScientistSearchSchedule>();
			search.Target = _lastKnownPosition.Value;
			return search;
		}

		// No intel — patrol
		var patrol = GetSchedule<CombatPatrolSchedule>();
		patrol.PatrolRadius = PatrolRadius;
		return patrol;
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy )
			return;

		Health -= damage.Damage;

		// If we can hear the attacker, treat their position as the last known location
		if ( damage.Attacker.IsValid() )
		{
			var dist = WorldPosition.Distance( damage.Attacker.WorldPosition );
			if ( dist <= Senses.HearingRange )
			{
				_lastKnownPosition = damage.Attacker.WorldPosition;
				_timeSinceLastSeen = 0;
			}
		}

		// Interrupt current schedule so we react immediately
		EndCurrentSchedule();

		if ( Health < 1f )
		{
			CreateRagdoll( GetAttackerVelocity( damage.Attacker ) );
			GameObject.Destroy();
		}
	}
}
