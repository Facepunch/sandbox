using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Rollermine.Schedules;

/// <summary>
/// Rollermine idles when no target is visible — waits briefly then re-evaluates.
/// </summary>
public class RollermineIdleSchedule : ScheduleBase
{
	protected override void OnStart()
	{
		AddTask( new Wait( Game.Random.Float( 1f, 2.5f ) ) );
	}

	protected override bool ShouldCancel()
	{
		return Npc.Senses.GetNearestVisible().IsValid();
	}
}
