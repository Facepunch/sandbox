using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// Wanders to random nearby points when no player is known.
/// </summary>
public class CombatPatrolSchedule : ScheduleBase
{
	private static readonly string[] PatrolLines =
	{
		"Stay sharp.",
		"Keep moving.",
		"All clear so far.",
		"Eyes open.",
		"Nothing yet.",
		"Where'd they go...",
		"Something's not right.",
		"I'll check over here.",
	};

	/// <summary>
	/// Maximum distance from current position to pick a patrol destination.
	/// </summary>
	public float PatrolRadius { get; set; } = 400f;

	protected override void OnStart()
	{
		var dest = GetPatrolDestination();
		AddTask( new MoveTo( dest, 15f ) );

		if ( Npc.Speech.CanSpeak && Game.Random.Float() < 0.2f )
			AddTask( new Say( Game.Random.FromArray( PatrolLines ), 2.5f ) );
		else
			AddTask( new Wait( Game.Random.Float( 1f, 2.5f ) ) );
	}

	protected override bool ShouldCancel()
	{
		return Npc.Senses.GetNearestVisible().IsValid();
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
