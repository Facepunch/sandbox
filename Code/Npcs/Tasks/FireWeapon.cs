using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Shoots a weapon at a target for a specific duration
/// </summary>
public class FireWeapon : TaskBase
{
	/// <summary>The weapon component to fire.</summary>
	public BaseWeapon Weapon { get; }

	/// <summary>The GameObject to aim at.</summary>
	public GameObject Target { get; }

	/// <summary>How long (seconds) to keep firing before the task completes.</summary>
	public float BurstDuration { get; }

	/// <summary>Body rotation speed (degrees/s scale) used while actively aiming. Higher than the default look speed.</summary>
	public float AimTurnSpeed { get; set; } = 8f;

	private TimeUntil _burstEnd;

	public FireWeapon( BaseWeapon weapon, GameObject target, float burstDuration = 1.5f )
	{
		Weapon = weapon;
		Target = target;
		BurstDuration = burstDuration;
	}

	protected override void OnStart()
	{
		_burstEnd = BurstDuration;
	}

	protected override TaskStatus OnUpdate()
	{
		if ( !Weapon.IsValid() )
			return TaskStatus.Failed;

		if ( !Target.IsValid() )
			return TaskStatus.Failed;

		RotateBodyTowardTarget();

		// Only fire once we're actually facing the target
		if ( Npc.Animation.IsFacingTarget() && Weapon.CanPrimaryAttack() )
		{
			Weapon.PrimaryAttack();
			Npc.Animation.TriggerAttack();
		}

		return _burstEnd ? TaskStatus.Success : TaskStatus.Running;
	}

	private void RotateBodyTowardTarget()
	{
		var toTarget = (Target.WorldPosition - Npc.WorldPosition).WithZ( 0 );
		if ( toTarget.LengthSquared < 1f ) return;

		var targetRot = Rotation.LookAt( toTarget.Normal, Vector3.Up );
		Npc.WorldRotation = Rotation.Lerp( Npc.WorldRotation, targetRot, AimTurnSpeed * Time.Delta );
	}
}
