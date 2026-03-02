using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Walk to a nearby prop, look at it, and maybe comment on it.
/// If the prop is small enough, pick it up, carry it around, then drop it.
/// </summary>
public class ScientistInspectPropSchedule : ScheduleBase
{
	private static readonly string[] InspectLines =
	[
		"Hmm, interesting...",
		"What's this?",
		"Fascinating.",
		"I should take notes.",
		"Now how did this get here?",
		"Curious...",
	];

	private static readonly string[] PickUpLines =
	[
		"I'll take this.",
		"Ooh, let me grab this.",
		"This is coming with me.",
		"Mine now.",
	];

	private static readonly string[] DropLines =
	[
		"There we go.",
		"That goes there.",
		"Alright, done with that.",
	];

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
		var speech = Npc.Layers.OfType<SpeechLayer>().FirstOrDefault();

		if ( isSmall )
		{
			// Comment on it, then pick it up
			if ( speech is not null && speech.CanSpeak )
			{
				var line = PickUpLines[Game.Random.Int( 0, PickUpLines.Length - 1 )];
				AddTask( new Say( line, 2f ) );
			}

			AddTask( new PickUpProp( PropTarget ) );

			Log.Info($"Picked up prop, moving!" );

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

			if ( speech is not null )
			{
				var line = DropLines[Game.Random.Int( 0, DropLines.Length - 1 )];
				AddTask( new Say( line, 2f ) );
			}
		}
		else
		{
			// Just observe the big prop
			if ( speech is not null && speech.CanSpeak && Game.Random.Float() < 0.5f )
			{
				var line = InspectLines[Game.Random.Int( 0, InspectLines.Length - 1 )];
				AddTask( new Say( line, 2.5f ) );
			}

			AddTask( new Wait( Game.Random.Float( 2f, 4f ) ) );
		}
	}

	protected override void OnEnd()
	{
		Npc.Animation.ClearLookTarget();
		Npc.Animation.ClearHeldProp();
	}
}
