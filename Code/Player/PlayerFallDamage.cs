/// <summary>
/// Apply fall damage to the player
/// </summary>
public class PlayerFallDamage : Component, IPlayerEvent
{
	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// Falling over this distance is considered a damaging fall
	/// </summary>
	[Property] public float MinimumFallDistance { get; set; } = 200;

	/// <summary>
	/// If you fall this distance it's death
	/// </summary>
	[Property] public float DeathFallDistance { get; set; } = 800;

	/// <summary>
	/// Multiply damage amount by this much
	/// </summary>
	[Property] public float DamageMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// Fall damage sound
	/// </summary>
	[Property] public SoundEvent FallSound { get; set; }


	int landCount = 0;

	[Rpc.Owner]
	private void PlayFallSound()
	{
		GameObject.PlaySound( FallSound );
	}

	void IPlayerEvent.OnLand( float distance, Vector3 velocity )
	{
		if ( !Networking.IsHost ) return;

		landCount++;

		if ( landCount < 1 )
			return;

		var damageScale = MathX.Remap( distance, MinimumFallDistance, DeathFallDistance, 0, 1 );
		int damageAmount = (int)(damageScale * 100 * DamageMultiplier);
		if ( damageAmount < 1 ) return;

		// play smashed legs on the ground sound

		if ( Player is IDamageable damage )
		{
			var dmg = new DamageInfo( damageAmount, Player.GameObject, null );
			dmg.Tags.Add( DamageTags.Fall );
			damage.OnDamage( dmg );

			PlayFallSound();
		}
	}
}
