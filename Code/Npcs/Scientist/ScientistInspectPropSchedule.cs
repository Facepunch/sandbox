using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Walk to a nearby prop, look at it, and maybe comment on it.
/// If the prop is small enough, pick it up, carry it around, then drop it.
/// </summary>
public sealed class ScientistInspectPropSchedule : ScheduleBase
{
	public GameObject PropTarget { get; set; }

	protected override void OnStart()
	{
		if ( !PropTarget.IsValid() ) return;

		// Persistently track the prop — the NPC will keep looking at it
		// throughout the entire schedule without needing repeated LookAt tasks.
		Npc.Animation.SetLookTarget( PropTarget );

		// Walk toward the prop — tracks it if it moves
		AddTask( new MoveTo( PropTarget, 40f ) );

		AddTask( new Wait( 1 ) );

		var bounds = PropTarget.GetBounds();
		var isSmall = bounds.Size.Length < 256;

		if ( isSmall )
		{
			// Pick it up, carry it around, then drop it.
			AddTask( new PickUpProp( PropTarget ) );

			// Try to find a reachable wander point on the navmesh
			var randomDir = Vector3.Random.WithZ( 0 ).Normal;
			var wanderTarget = PropTarget.WorldPosition + randomDir * Game.Random.Float( 150f, 300f );

			if ( Npc.Scene.NavMesh.GetClosestPoint( wanderTarget ) is Vector3 navPoint )
			{
				AddTask( new MoveTo( navPoint, 15f ) );
			}

			// Always added — even if MoveTo was skipped or would have failed,
			// the NPC still holds the prop for a bit, then drops it properly.
			AddTask( new Wait( Game.Random.Float( 3f, 6f ) ) );

			AddTask( new DropProp( PropTarget ) );
		}
		else
		{
			// Just observe the big prop.
			AddTask( new Wait( Game.Random.Float( 2f, 4f ) ) );
		}
	}

	protected override void OnEnd()
	{
		Npc.Animation.ClearLookTarget();
		Npc.Animation.ClearHeldProp();
	}
}
