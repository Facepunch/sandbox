using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

public class ScientistSearchSchedule : ScheduleBase
{
	public Vector3 Target { get; set; }

	protected override void OnStart()
	{
		AddTask( new MoveTo( Target ) );
		AddTask( new LookAt( Target ) );
		AddTask( new Wait( 1f ) );
	}
}
