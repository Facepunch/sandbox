using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

public class ScientistInvestigateSchedule : ScheduleBase
{
	public GameObject Target { get; set; }

	protected override void OnStart()
	{
		// Turn to face the target
		AddTask( new LookAt( Target ) );

		// Wait a bit
		AddTask( new Wait( 1.5f ) );
	}
}
