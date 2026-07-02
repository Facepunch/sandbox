using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.Scientist;

public sealed class ScientistNpc : Npc, Component.IPressable
{
	/// <summary>
	/// Current fear level (0–1). Computed from peak fear and time since last hurt.
	/// </summary>
	public float AfraidLevel
	{
		get
		{
			if ( _peakFear <= 0f ) return 0f;
			if ( _timeSinceHurt <= FearGracePeriod ) return _peakFear;

			var decayTime = _timeSinceHurt - FearGracePeriod;
			return MathF.Max( _peakFear - decayTime * FearDecayRate, 0f );
		}
	}

	/// <summary>
	/// Seconds at full fear before decay begins.
	/// </summary>
	[Property, Group( "Balance" )]
	private float FearGracePeriod { get; set; } = 3f;

	/// <summary>
	/// Fear units lost per second after the grace period.
	/// </summary>
	[Property, Group( "Balance" )]
	private float FearDecayRate { get; set; } = 0.15f;

	private float _peakFear;
	private GameObject _attacker;
	private TimeSince _timeSinceHurt;
	private bool _isFleeing;
	private TimeSince _timeSinceStruggling;

	// Voice lines by situation. Each SoundEvent picks a random clip. These replace the old
	// on-screen text -- the scientist speaks instead.
	[Property, Group( "Voice" )] public SoundEvent FollowVoice { get; set; }
	[Property, Group( "Voice" )] public SoundEvent StayVoice { get; set; }
	[Property, Group( "Voice" )] public SoundEvent ScaredVoice { get; set; }
	[Property, Group( "Voice" )] public SoundEvent IdleVoice { get; set; }
	[Property, Group( "Voice" )] public SoundEvent StuckVoice { get; set; }

	/// <summary>
	/// The player this scientist is following, if any. Press USE on the scientist to toggle.
	/// </summary>
	[Sync]
	public GameObject Leader { get; set; }

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		return Leader.IsValid()
			? new IPressable.Tooltip( "Stop following", "person_off", DisplayName )
			: new IPressable.Tooltip( "Follow me", "follow_the_signs", DisplayName );
	}

	bool IPressable.CanPress( IPressable.Event e ) => true;

	bool IPressable.Press( IPressable.Event e )
	{
		ToggleFollow( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	private void ToggleFollow( GameObject presserObject )
	{
		if ( !presserObject.IsValid() )
			return;

		var player = presserObject.Root.GetComponent<Player>();
		if ( !player.IsValid() )
			return;

		if ( Leader == player.GameObject )
		{
			Leader = null;
			SayVoice( StayVoice, force: true );
		}
		else
		{
			Leader = player.GameObject;
			_timeSinceStruggling = 0;
			SayVoice( FollowVoice, force: true );
		}

		// Re-think now so following starts/stops immediately.
		EndCurrentSchedule();
	}

	// Play a voice line. Ambient lines respect the speech cooldown; USE responses force through.
	private void SayVoice( SoundEvent voice, bool force = false )
	{
		if ( voice is null ) return;
		if ( !force && !Speech.CanSpeak ) return;

		Speech.Say( voice );
	}

	// Defenceless -- flees anything dangerous, indifferent to everyone else.
	public override string Faction => Factions.Citizen;

	protected override void SetupRelationships() => Fears( Factions.Enemy, Factions.Monster );

	public override ScheduleBase GetSchedule()
	{
		var fear = AfraidLevel;

		// Fear fully decayed — clear state
		if ( fear <= 0f && _peakFear > 0f )
		{
			_peakFear = 0f;
			_attacker = null;
		}

		// Afraid — flee from the attacker
		if ( fear > 0f && _attacker.IsValid() )
		{
			if ( !_isFleeing )
			{
				_isFleeing = true;
				_attacker.GetComponent<Player>()?.PlayerData?.AddStat( "npc.scientist.scare" );
			}

			SayVoice( ScaredVoice );

			var flee = GetSchedule<ScientistFleeSchedule>();
			flee.Source = _attacker;
			flee.PanicLevel = fear;
			return flee;
		}

		// Flee from anything we're scared of on sight, before it even hurts us.
		var threat = Senses.GetNearestVisible( Disposition.Fearful );
		if ( threat.IsValid() )
		{
			SayVoice( ScaredVoice );

			var sightFlee = GetSchedule<ScientistFleeSchedule>();
			sightFlee.Source = threat;
			sightFlee.PanicLevel = 0.5f;
			return sightFlee;
		}

		// Not fleeing anything.
		_isFleeing = false;

		// Following a player who recruited us with USE. (Fear above takes priority, so we
		// still cower from danger, then resume following once it passes.)
		if ( Leader.IsValid() )
		{
			// Keep track of whether we're keeping up; if we fall too far behind for too long,
			// give up and stay put.
			if ( WorldPosition.Distance( Leader.WorldPosition ) < 350f )
				_timeSinceStruggling = 0;

			if ( _timeSinceStruggling > 6f )
			{
				Leader = null;
				SayVoice( StuckVoice, force: true );
				return GetIdleSchedule();
			}

			var follow = GetSchedule<FollowSchedule>();
			follow.Target = Leader;
			return follow;
		}

		return GetIdleSchedule();
	}

	/// <summary>
	/// Pick a random idle behavior so the scientist doesn't just stand around.
	/// </summary>
	private ScheduleBase GetIdleSchedule()
	{
		// Occasionally mutter something while pottering about.
		SayVoice( IdleVoice );

		var roll = Game.Random.Float();

		if ( roll < 0.35f )
		{ 
			return GetSchedule<ScientistWanderSchedule>();
		}

		if ( roll < 0.60f )
		{
			var prop = FindNearbyProp();
			if ( prop.IsValid() )
			{
				var inspect = GetSchedule<ScientistInspectPropSchedule>();
				inspect.PropTarget = prop;
				return inspect;
			}
		}

		return GetSchedule<ScientistIdleSchedule>();
	}

	/// <summary>
	/// Find the nearest prop within range to inspect.
	/// </summary>
	private GameObject FindNearbyProp()
	{
		// TODO: I feel like the senses layer should be able to hand all of this in a cost effective way.

		var nearby = Scene.FindInPhysics( new Sphere( WorldPosition, 2048 ) );

		GameObject best = null;
		float bestDist = float.MaxValue;

		foreach ( var obj in nearby )
		{
			if ( obj == GameObject ) continue;
			if ( obj.GetComponent<Prop>() is null ) continue;

			var dist = WorldPosition.Distance( obj.WorldPosition );
			if ( dist < bestDist )
			{
				bestDist = dist;
				best = obj;
			}
		}

		return best;
	}

	protected override void OnHurt( in DamageInfo damage )
	{
		// Escalate fear — each hit stacks, clamped to 1.
		_peakFear = MathF.Min( _peakFear + damage.Damage / 50f, 1f );
		_attacker = damage.Attacker;
		_timeSinceHurt = 0;

		// React immediately so flee picks up.
		EndCurrentSchedule();
	}

	protected override void Die( in DamageInfo damage )
	{
		damage.Attacker?.GetComponent<Player>()?.PlayerData?.AddStat( "npc.scientist.kill" );
		base.Die( damage );
	}
}
