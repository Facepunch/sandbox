/// <summary>
/// An extension of <see cref="DamageInfo"/> which has extra properties, and utilities for networking.
/// </summary>
public sealed class DeathmatchDamageInfo : DamageInfo
{
	/// <summary>
	/// The player that caused this
	/// </summary>
	public Guid InstigatorId { get; set; }

	/// <summary>
	/// Where did this damage originate from?
	/// </summary>
	public Vector3 Origin { get; set; }

	/// <summary>
	/// Create damage info, inflicted by a player (or bot)
	/// </summary>
	public DeathmatchDamageInfo( float damage, Player attacker, GameObject weapon = default )
		: this( damage, attacker.IsValid() ? attacker.GameObject : null, weapon )
	{
		InstigatorId = attacker?.PlayerId ?? Guid.Empty;
	}

	/// <summary>
	/// Create damage info, inflicted from an object
	/// </summary>
	public DeathmatchDamageInfo( float damage, GameObject attacker, GameObject weapon = default ) : base( damage, attacker, weapon )
	{

	}

	public bool IsGibType()
	{
		return Tags.HasAny( DamageTags.Crush, DamageTags.Explosion, DamageTags.GibAlways );
	}
}

public static class DamageTags
{
	public const string Headshot = "head";
	public const string Crush = "crush";
	public const string Explosion = "explosion";
	public const string Shock = "shock";
	public const string Fall = "fall";
	public const string GibAlways = "gib_always";
	public const string FullSelfDamage = "full_self_damage";
}

public static class DamageExtensions
{
	public static void Damage( this Component.IDamageable damageable, DeathmatchDamageInfo damageInfo )
	{
		Assert.True( Networking.IsHost, "Only the host can deal damage!" );
		if ( !Networking.IsHost ) return;

		Assert.NotNull( damageInfo, "Null damageInfo!" );
		if ( damageInfo is null ) return;

		if ( damageable is Component comp )
		{
			if ( comp.GameObject.IsDestroyed ) return;
		}

		damageable.OnDamage( damageInfo );
	}
}
