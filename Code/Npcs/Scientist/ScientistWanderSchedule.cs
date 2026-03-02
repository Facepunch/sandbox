using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Wander to a random nearby point, avoiding player directions.
/// </summary>
public class ScientistWanderSchedule : ScheduleBase
{
	private static readonly string[] WanderLines =
	[
		"Let me check over here...",
		"Hmm, what's over there?",
		"I should stretch my legs.",
		"*whistles*",
	];

	protected override void OnStart()
	{
		var randomDir = Vector3.Random.WithZ( 0 ).Normal;

		// Don't wander toward nearby players
		var nearest = Npc.Senses.Nearest;
		if ( nearest.IsValid() )
		{
			var toPlayer = (nearest.WorldPosition - GameObject.WorldPosition).WithZ( 0 ).Normal;
			if ( randomDir.Dot( toPlayer ) > 0.3f )
			{
				// Flip away from the player with some randomness
				randomDir = (-toPlayer + Vector3.Random.WithZ( 0 ).Normal * 0.5f).WithZ( 0 ).Normal;
			}
		}

		var wanderTarget = GameObject.WorldPosition + randomDir * Game.Random.Float( 150f, 350f );

		AddTask( new MoveTo( wanderTarget, 15f ) );

		// Occasionally say something while walking
		var speech = Npc.Layers.OfType<SpeechLayer>().FirstOrDefault();
		if ( speech is not null && speech.CanSpeak && Game.Random.Float() < 0.2f )
		{
			var line = WanderLines[Game.Random.Int( 0, WanderLines.Length - 1 )];
			AddTask( new Say( line, 2.5f ) );
		}

		AddTask( new Wait( Game.Random.Float( 1f, 2f ) ) );
	}
}
