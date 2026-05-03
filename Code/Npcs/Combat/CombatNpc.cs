using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// A combat NPC that searches for players, advances on them, fires in bursts, and repositions.
/// When friendly, follows players and engages hostile NPCs instead.
/// </summary>
public class CombatNpc : Npc, Component.IDamageable
{
	private static readonly string[] PainLines =
	{
		"Argh!",
		"They got me!",
		"I'm hit!",
		"Taking fire!",
		"Ugh!",
	};

	private static readonly string[] DeathLines =
	{
		"Tell them... I fought...",
		"Not like this...",
		"I can't...",
	};

	/// <summary>
	/// When true, this NPC is friendly to players and will follow them, engaging hostile NPCs.
	/// When false, this NPC targets players and friendly NPCs.
	/// </summary>
	[Property, ClientEditable, Sync]
	public bool Friendly { get; set; } = false;

	[Property, ClientEditable, Range( 1, 250 ), Sync]
	public float Health { get; set; } = 100f;

	/// <summary>
	/// The weapon this NPC uses to attack.
	/// </summary>
	[Property]
	public BaseWeapon Weapon { get; set; }

	[Property, Group( "Balance" ), Range( 512, 4096 ), Step( 1 ), ClientEditable, Sync]
	public float AttackRange { get; set; } = 1024f;

	[Property, Group( "Balance" ), Range( 90, 250f ), Step( 1 ), ClientEditable, Sync]
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

	/// <summary>
	/// How far a friendly NPC will follow a player before stopping.
	/// </summary>
	[Property, Group( "Balance" )]
	public float FollowDistance { get; set; } = 150f;

	private Vector3? _lastKnownPosition;
	private TimeSince _timeSinceLastSeen;

	protected override void OnStart()
	{
		base.OnStart();

		if ( !IsProxy )
		{
			Senses.ScanTags = new TagSet { "player", "friendly_npc", "hostile_npc" };

			if ( Friendly )
			{
				GameObject.Tags.Add( "friendly_npc" );
				Senses.TargetTags = new TagSet { "hostile_npc" };
			}
			else
			{
				GameObject.Tags.Add( "hostile_npc" );
				Senses.TargetTags = new TagSet { "player", "friendly_npc" };
			}
		}

		if ( Weapon.IsValid() && Renderer.IsValid() )
		{
			Weapon.CreateWorldModel( Renderer );

			if ( !IsProxy )
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

		// Search last known position if recent enough
		if ( _lastKnownPosition.HasValue && _timeSinceLastSeen < SearchTimeout )
		{
			var search = GetSchedule<ScientistSearchSchedule>();
			search.Target = _lastKnownPosition.Value;
			return search;
		}

		// Friendly NPCs follow the nearest player when idle
		if ( Friendly )
		{
			var follow = GetSchedule<CombatFollowSchedule>();
			follow.FollowDistance = FollowDistance;
			return follow;
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

		if ( Health < 1f )
		{
			if ( Speech.CanSpeak )
				Speech.Say( Game.Random.FromArray( DeathLines ), 2f );

			Die( damage );
			return;
		}

		if ( Speech.CanSpeak && Game.Random.Float() < 0.5f )
			Speech.Say( Game.Random.FromArray( PainLines ), 1.5f );

		// Interrupt current schedule so we react immediately
		EndCurrentSchedule();
	}
}
