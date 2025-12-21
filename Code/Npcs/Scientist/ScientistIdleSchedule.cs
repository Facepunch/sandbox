using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

public class ScientistIdleSchedule : ScheduleBase
{
	protected override void OnStart()
	{
		// look around randomly
		var randomDir = Vector3.Random.Normal;
		var lookTarget = GameObject.WorldPosition + randomDir * 100f;
		AddTask( new LookAt( lookTarget ) );

		// wait a bit, with random deviation
		AddTask( new Wait( Game.Random.Float( 1f, 3f ) ) );
	}
}
