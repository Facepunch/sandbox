namespace Sandbox.Npcs;

public partial class Npc : Component
{
	/// <summary>
	/// The faction this NPC belongs to -- its team identity, used for the "same team is
	/// friendly" default. Always included in <see cref="GetTraits"/>. Override per NPC type.
	/// </summary>
	public virtual string Faction => "neutral";

	/// <summary>
	/// What this NPC is and how it reacts to others, declared by trait. Override per NPC type.
	/// </summary>
	protected virtual Dispositions Dispositions => DefaultDispositions;
	static readonly Dispositions DefaultDispositions = new();

	private Dispositions _rules;
	private HashSet<string> _traits;
	readonly Dictionary<GameObject, Disposition> _dispositionOverrides = new();

	private Dispositions Rules => _rules ??= Dispositions;

	/// <summary>
	/// All traits describing this NPC, including its faction. Built once and cached.
	/// </summary>
	public IReadOnlySet<string> GetTraits()
	{
		_traits ??= new HashSet<string>( Rules.Traits ) { Faction };
		return _traits;
	}

	/// <summary>
	/// How this NPC reacts to an entity with the given traits. The default applies the
	/// declarative <see cref="Dispositions"/> rules; override for fully custom logic.
	/// </summary>
	protected virtual Disposition GetDispositionTo( IReadOnlySet<string> traits )
	{
		if ( traits.Contains( Faction ) )
			return Disposition.Friendly;

		var rules = Rules;
		if ( MatchesAny( traits, rules.Hostile ) ) return Disposition.Hostile;
		if ( MatchesAny( traits, rules.Fearful ) ) return Disposition.Fearful;
		if ( MatchesAny( traits, rules.Friendly ) ) return Disposition.Friendly;

		return Disposition.Neutral;
	}

	/// <summary>
	/// How this NPC currently regards another entity. A per-entity override wins; otherwise
	/// it's decided by <see cref="GetDispositionTo"/> from the other's traits.
	/// </summary>
	public Disposition GetDisposition( GameObject other )
	{
		if ( !other.IsValid() || other == GameObject )
			return Disposition.Friendly;

		if ( _dispositionOverrides.TryGetValue( other, out var over ) )
			return over;

		return GetDispositionTo( GetTraitsOf( other ) );
	}

	/// <summary>
	/// Override how this NPC regards a specific entity, ignoring its trait rules --
	/// e.g. a cop turning hostile toward someone it saw commit a crime.
	/// </summary>
	public void SetDisposition( GameObject other, Disposition disposition )
	{
		if ( !other.IsValid() )
			return;

		_dispositionOverrides[other] = disposition;
	}

	/// <summary>
	/// Remove a per-entity override, falling back to the trait rules.
	/// </summary>
	public void ClearDisposition( GameObject other )
	{
		_dispositionOverrides.Remove( other );
	}

	/// <summary>
	/// Resolve the traits of any entity -- an NPC's own traits, or a player's. Returns an
	/// empty set for things that don't take part in relationships.
	/// </summary>
	public static IReadOnlySet<string> GetTraitsOf( GameObject go )
	{
		if ( !go.IsValid() )
			return NoTraits;

		var npc = go.GetComponent<Npc>() ?? go.Root?.GetComponent<Npc>();
		if ( npc.IsValid() )
			return npc.GetTraits();

		if ( go.Tags.Has( Trait.Player ) || (go.Root.IsValid() && go.Root.Tags.Has( Trait.Player )) )
			return PlayerTraits;

		return NoTraits;
	}

	static readonly HashSet<string> PlayerTraits = new() { Trait.Player, Trait.Living };
	static readonly HashSet<string> NoTraits = new();

	static bool MatchesAny( IReadOnlySet<string> traits, List<string> any )
	{
		foreach ( var trait in any )
		{
			if ( traits.Contains( trait ) )
				return true;
		}

		return false;
	}
}
