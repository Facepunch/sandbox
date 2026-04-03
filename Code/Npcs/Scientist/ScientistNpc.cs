using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.Scientist;

public class ScientistNpc : Npc, Component.IDamageable
{
	[Property, ClientEditable, Range( 1, 100 ), Sync]
	public float Health { get; set; } = 100f;

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

			var flee = GetSchedule<ScientistFleeSchedule>();
			flee.Source = _attacker;
			flee.PanicLevel = fear;
			return flee;
		}

		_isFleeing = false;

		return GetIdleSchedule();
	}

	/// <summary>
	/// Pick a random idle behavior so the scientist doesn't just stand around.
	/// </summary>
	private ScheduleBase GetIdleSchedule()
	{
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

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy )
			return;

		Health -= damage.Damage;

		// Escalate fear — each hit stacks, clamped to 1
		_peakFear = MathF.Min( _peakFear + damage.Damage / 50f, 1f );
		_attacker = damage.Attacker;
		_timeSinceHurt = 0;

		// Interrupt whatever we're doing so flee picks up immediately
		EndCurrentSchedule();

		if ( Health < 1 )
		{
			_attacker?.GetComponent<Player>()?.PlayerData?.AddStat( "npc.scientist.kill" );
			Die( damage );
		}
	}
}
