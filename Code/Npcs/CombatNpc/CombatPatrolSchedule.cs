using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// Wanders to random nearby points when no player is known.
/// </summary>
public class CombatPatrolSchedule : ScheduleBase
{
	/// <summary>
	/// Maximum distance from current position to pick a patrol destination.
	/// </summary>
	public float PatrolRadius { get; set; } = 400f;

	protected override void OnStart()
	{
		var dest = GetPatrolDestination();
		AddTask( new MoveTo( dest, 15f ) );
		AddTask( new Wait( Game.Random.Float( 1f, 2.5f ) ) );
	}

	private Vector3 GetPatrolDestination()
	{
		var dir = Vector3.Random.WithZ( 0 ).Normal;
		var dist = Game.Random.Float( PatrolRadius * 0.3f, PatrolRadius );
		var candidate = Npc.WorldPosition + dir * dist;

		if ( Npc.Scene.NavMesh.GetClosestPoint( candidate ) is { } nav )
			return nav;

		return candidate;
	}
}
