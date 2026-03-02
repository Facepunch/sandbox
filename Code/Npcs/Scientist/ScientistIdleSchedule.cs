using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Idle in place — glance around in a natural forward arc and occasionally mutter.
/// </summary>
public class ScientistIdleSchedule : ScheduleBase
{
	private static readonly string[] IdleLines =
	[
		"...",
		"Hmm.",
		"*yawn*",
		"What a day.",
		"Need more coffee.",
	];

	protected override void OnStart()
	{
		// Pick a horizontal direction within ±90° of where we're already facing
		var forward = GameObject.WorldRotation.Forward.WithZ( 0 ).Normal;
		var yawOffset = Game.Random.Float( -90f, 90f );
		var lookDir = Rotation.FromAxis( Vector3.Up, yawOffset ) * forward;
		var lookTarget = GameObject.WorldPosition + lookDir * 100f;
		AddTask( new LookAt( lookTarget ) );

		// occasionally mutter something
		var speech = Npc.Layers.OfType<SpeechLayer>().FirstOrDefault();
		if ( speech is not null && speech.CanSpeak && Game.Random.Float() < 0.15f )
		{
			var line = IdleLines[Game.Random.Int( 0, IdleLines.Length - 1 )];
			AddTask( new Say( line, 2f ) );
		}

		// wait a bit, with random deviation
		AddTask( new Wait( Game.Random.Float( 1f, 3f ) ) );
	}
}
