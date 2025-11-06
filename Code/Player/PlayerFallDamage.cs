/// <summary>
/// Apply fall damage to the player
/// </summary>
public class PlayerFallDamage : Component, IPlayerEvent
{
	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// Fatal fall speed, you will die if you fall at or above this speed
	/// </summary>
	[Property] public float FatalFallSpeed { get; set; } = 1536.0f;

	/// <summary>
	/// Maximum safe fall speed, you won't take damage at or below this speed
	/// </summary>
	[Property] public float MaxSafeFallSpeed { get; set; } = 512.0f;

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

		var fallSpeed = Math.Abs( velocity.z );

		if ( fallSpeed <= MaxSafeFallSpeed )
			return;

		var damageAmount = MathX.Remap( fallSpeed, MaxSafeFallSpeed, FatalFallSpeed, 0f, 100f ) * DamageMultiplier;
		if ( damageAmount < 1 ) return;

		if ( Player is IDamageable damage )
		{
			var dmg = new DamageInfo( damageAmount.CeilToInt(), Player.GameObject, null );
			dmg.Tags.Add( DamageTags.Fall );
			damage.OnDamage( dmg );

			PlayFallSound();
		}
	}
}
