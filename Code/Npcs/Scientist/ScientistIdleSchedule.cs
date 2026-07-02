using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Idle in place — glance around in a natural forward arc and occasionally mutter.
/// </summary>
public sealed class ScientistIdleSchedule : ScheduleBase
{
	protected override void OnStart()
	{
		// Pick a horizontal direction within ±90° of where we're already facing
		var forward = GameObject.WorldRotation.Forward.WithZ( 0 ).Normal;
		var yawOffset = Game.Random.Float( -90f, 90f );
		var lookDir = Rotation.FromAxis( Vector3.Up, yawOffset ) * forward;
		var lookTarget = GameObject.WorldPosition + lookDir * 100f;
		AddTask( new LookAt( lookTarget ) );

		// wait a bit, with random deviation
		AddTask( new Wait( Game.Random.Float( 1f, 3f ) ) );
	}
}
