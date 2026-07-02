using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Follow a target around, staying within a set distance. Re-evaluates each cycle so it
/// tracks a moving target. Higher-priority schedules (like fleeing danger) preempt it.
/// </summary>
public sealed class FollowSchedule : ScheduleBase
{
	public GameObject Target { get; set; }
	public float FollowDistance { get; set; } = 120f;

	public override int Priority => SchedulePriority.Ambient;

	protected override void OnStart()
	{
		if ( !Target.IsValid() )
		{
			AddTask( new Wait( 0.5f ) );
			return;
		}

		// Keep looking at whoever we're following.
		Npc.Animation.SetLookTarget( Target );

		AddTask( new MoveTo( Target, FollowDistance ) );
		AddTask( new Wait( 0.5f ) );
	}

	protected override void OnEnd()
	{
		Npc.Animation.ClearLookTarget();
	}

	protected override bool ShouldCancel() => !Target.IsValid();
}
