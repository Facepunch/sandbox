using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Move to a point, look around, and wait -- used to check out a last-known position or a
/// heard disturbance. Re-spotting a target preempts this with a higher-priority schedule.
/// </summary>
public sealed class InvestigateSchedule : ScheduleBase
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
