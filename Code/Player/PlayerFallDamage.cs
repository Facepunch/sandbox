/// <summary>
/// Apply fall damage to the player
/// </summary>
public class PlayerFallDamage : Component, Local.IPlayerEvents
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

	[Rpc.Owner]
	private void PlayFallSound()
	{
		GameObject.PlaySound( FallSound );
	}

	void Local.IPlayerEvents.OnLand( float distance, Vector3 velocity )
	{
		var fallSpeed = Math.Abs( velocity.z );

		if ( fallSpeed <= MaxSafeFallSpeed )
			return;

		var damageAmount = MathX.Remap( fallSpeed, MaxSafeFallSpeed, FatalFallSpeed, 0f, 100f ) * DamageMultiplier;
		if ( damageAmount < 1 ) return;

		if ( damageAmount >= Player.Health )
			Player.PlayerData?.AddStat( "player.fall.death" );

		TakeFallDamage( damageAmount );
	}


	[Rpc.Broadcast]
	public void TakeFallDamage( float amount )
	{
		if ( !Networking.IsHost ) return;


		if ( Player is IDamageable damage )
		{
			var dmg = new DamageInfo( amount.CeilToInt(), Player.GameObject, null );
			dmg.Tags.Add( DamageTags.Fall );
			damage.OnDamage( dmg );

			PlayFallSound();
		}
	}
}
