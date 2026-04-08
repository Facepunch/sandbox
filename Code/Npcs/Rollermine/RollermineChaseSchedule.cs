using Sandbox.Npcs.Rollermine.Tasks;

namespace Sandbox.Npcs.Rollermine.Schedules;

/// <summary>
/// Rollermine chase: roll toward target then leap at it.
/// On completion the schedule ends naturally, GetSchedule re-picks it and loops.
/// </summary>
public class RollermineChaseSchedule : ScheduleBase
{
	protected override void OnStart()
	{
		(Npc as RollermineNpc)?.SetHunting( true );
		AddTask( new RollermineRollTask() );
		AddTask( new RollermineLeapTask() );
	}

	protected override void OnEnd()
	{
		(Npc as RollermineNpc)?.SetHunting( false );
	}

	protected override void OnCancelled()
	{
		(Npc as RollermineNpc)?.SetHunting( false );
	}

	protected override bool ShouldCancel()
	{
		return !Npc.Senses.GetNearestVisible().IsValid();
	}
}
