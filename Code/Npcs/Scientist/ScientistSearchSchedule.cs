using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

public sealed class ScientistSearchSchedule : ScheduleBase
{
	public Vector3 Target { get; set; }

	public override int Priority => SchedulePriority.Alert;

	protected override void OnStart()
	{
		AddTask( new MoveTo( Target ) );
		AddTask( new LookAt( Target ) );
		AddTask( new Wait( 1f ) );
	}

}
