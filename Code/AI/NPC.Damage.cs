using System.Threading;

namespace Sandbox.AI;

public sealed partial class Npc : Component.IDamageable
{
	/// <summary>
	/// How healthy is this npc
	/// </summary>
	[Property, Range( 1, 100f )] public float Health { get; set; } = 100f;

	/// <summary>
	/// The max hp, used for thresholds for fleeing right now
	/// </summary>
	[Property, Range( 1, 100f )] public float MaxHealth { get; set; } = 100f;

	public void OnDamage( in DamageInfo info )
	{
		if ( Health <= 0 )
			return;

		if ( info.Attacker.GetComponent<IActor>() is var attacker && attacker != this )
		{
			_attackers.Add( attacker );
		}

		Health -= info.Damage;

		// Increase scared level based on damage taken
		var scareIncrease = info.Damage * DamageScareMultiplier;
		AddScare( scareIncrease );

		if ( Health >= 1 )
			return;

		CancelTasks();
		CreateRagdoll();
		GameObject.Destroy();
	}
}
