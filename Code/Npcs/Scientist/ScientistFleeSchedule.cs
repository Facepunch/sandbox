using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Simple flee - just move away quickly, then complete
/// </summary>
public class ScientistFleeSchedule : ScheduleBase
{
	public GameObject Source { get; set; }

	protected override void OnStart()
	{
		if ( !Source.IsValid() ) return;

		var awayDirection = (GameObject.WorldPosition - Source.WorldPosition).Normal;
		var fleeTarget = GameObject.WorldPosition + awayDirection * 150f;

		AddTask( new MoveTo( fleeTarget ) );
	}
}
