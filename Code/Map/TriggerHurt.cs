/// <summary>
/// Deals damage to objects inside
/// </summary>
[Category( "Gameplay" ), Icon( "medical_services" ), EditorHandle( Icon = "🤕" )]
public sealed class TriggerHurt : Component
{
	/// <summary>
	/// If not empty, the target must have one of these tags
	/// </summary>
	[Property, Group( "Damage" )] public TagSet DamageTags { get; set; } = new();

	/// <summary>
	/// How much damage to apply
	/// </summary>
	[Property, Group( "Damage" )] public float Damage { get; set; } = 10.0f;

	/// <summary>
	/// The delay between applying the damage
	/// </summary>
	[Property, Group( "Damage" )] public float Rate { get; set; } = 1.0f;

	/// <summary>
	/// If not empty, the target must have one of these tags
	/// </summary>
	[Property, Group( "Target" )] public TagSet Include { get; set; } = new();

	/// <summary>
	/// If not empty, the target must not have one of these tags
	/// </summary>
	[Property, Group( "Target" )] public TagSet Exclude { get; set; } = new();

	TimeSince timeSinceDamage = 0.0f;
	Collider Collider => GetComponent<Collider>();

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( timeSinceDamage < Rate ) return;
		if ( !Collider.IsValid() ) return;

		timeSinceDamage = 0;

		foreach ( var touching in Collider.Touching.SelectMany( x => x.GetComponentsInParent<IDamageable>().Distinct() ) )
		{
			if ( touching is not Component target ) continue;

			if ( !Exclude.IsEmpty && target.GameObject.Tags.HasAny( Exclude ) ) continue;
			if ( !Include.IsEmpty && !target.GameObject.Tags.HasAny( Include ) ) continue;

			DeathmatchDamageInfo damageInfo;
			if ( target is Player player )
			{
				damageInfo = new DeathmatchDamageInfo( Damage, target as Player, GameObject );
			}
			else
			{
				damageInfo = new DeathmatchDamageInfo( Damage, GameObject, GameObject );
			}

			damageInfo.Tags.Add( DamageTags );
			touching.Damage( damageInfo );
		}
	}
}
