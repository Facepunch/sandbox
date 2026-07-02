using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// A combat NPC that searches for players, advances on them, fires in bursts, and repositions.
/// When friendly, follows players and engages hostile NPCs instead.
/// </summary>
public class CombatNpc : Npc
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

	/// <summary>
	/// The weapon this NPC uses to attack.
	/// </summary>
	[Property]
	public BaseSandboxWeapon Weapon { get; set; }

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

		if ( Weapon.IsValid() && Renderer.IsValid() )
		{
			Weapon.CreateWorldModel( Renderer );

			if ( !IsProxy )
				Animation.SetHoldType( Weapon.HoldType );
		}
	}

	// Friendly soldiers fight alongside the player; the rest are enemies.
	public override string Faction => Friendly ? Factions.Ally : Factions.Enemy;

	protected override void SetupRelationships()
	{
		if ( Friendly )
		{
			Likes( Factions.Player );
			Hates( Factions.Enemy, Factions.Monster );
		}
		else
		{
			Hates( Factions.Player, Factions.Ally, Factions.Citizen );
		}
	}

	public override ScheduleBase GetSchedule()
	{
		var visible = Senses.GetBestTarget();

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
			var search = GetSchedule<InvestigateSchedule>();
			search.Target = _lastKnownPosition.Value;
			return search;
		}

		// Heard gunfire or some other disturbance but can't see the source -- go check it out.
		if ( Senses.Disturbance is { } disturbance )
		{
			var investigate = GetSchedule<InvestigateSchedule>();
			investigate.Target = disturbance.Position;
			return investigate;
		}

		// Friendly NPCs follow the nearest player when idle
		if ( Friendly )
		{
			var player = Senses.GetNearestVisible( "player" );
			if ( player.IsValid() )
			{
				var follow = GetSchedule<FollowSchedule>();
				follow.Target = player;
				follow.FollowDistance = FollowDistance;
				return follow;
			}
		}

		// No intel — patrol
		var patrol = GetSchedule<CombatPatrolSchedule>();
		patrol.PatrolRadius = PatrolRadius;
		return patrol;
	}

	protected override void OnHurt( in DamageInfo damage )
	{
		if ( damage.Attacker.IsValid() )
		{
			// Turn on whoever hurt us, even a former ally, and prioritise them as a target.
			// This is the runtime "turn nasty" path the crime/aggro systems reuse.
			SetDisposition( damage.Attacker, Disposition.Hostile, priority: 10 );

			// If we can hear the attacker, treat their position as the last known location.
			if ( WorldPosition.Distance( damage.Attacker.WorldPosition ) <= Senses.HearingRange )
			{
				_lastKnownPosition = damage.Attacker.WorldPosition;
				_timeSinceLastSeen = 0;
			}
		}

		if ( Health >= 1f && Speech.CanSpeak && Game.Random.Float() < 0.5f )
			Speech.Say( Game.Random.FromArray( PainLines ), 1.5f );

		// React immediately.
		EndCurrentSchedule();
	}

	protected override void Die( in DamageInfo damage )
	{
		if ( Speech.CanSpeak )
			Speech.Say( Game.Random.FromArray( DeathLines ), 2f );

		DropWeapon();

		base.Die( damage );
	}

	/// <summary>
	/// Drop the held weapon into the world where the hand was, so it survives the NPC's death and can
	/// be picked up.
	/// </summary>
	private void DropWeapon()
	{
		if ( !Weapon.IsValid() )
			return;

		var position = Weapon.WorldModel.IsValid()
			? Weapon.WorldModel.WorldPosition
			: WorldPosition + Vector3.Up * 32f;

		Weapon.SpawnDroppedPickup( position, Vector3.Up * 50f );
	}
}
