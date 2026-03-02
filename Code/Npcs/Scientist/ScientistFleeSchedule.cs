using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Panic flee — scream while sprinting away from the source.
/// </summary>
public class ScientistFleeSchedule : ScheduleBase
{
	private static readonly string[] PanicLines =
	[
		"AHHH!",
		"Don't hurt me!",
		"Help! HELP!",
		"Stay away from me!",
		"I'm just a scientist!",
		"Please, no!",
		"Somebody help!",
		"Oh god oh god oh god!",
		"What did I do?!",
		"Leave me alone!",
	];

	public GameObject Source { get; set; }

	/// <summary>
	/// 0–1 panic intensity. Higher values mean faster speed and longer flee distance.
	/// </summary>
	public float PanicLevel { get; set; } = 0.5f;

	protected override void OnStart()
	{
		if ( !Source.IsValid() ) return;

		// Sprint speed scales with panic (200–350)
		Npc.Navigation.WishSpeed = 200f + 150f * PanicLevel;

		// Scream immediately — fires as the NPC starts running, not sequentially
		var line = PanicLines[Game.Random.Int( 0, PanicLines.Length - 1 )];
		Npc.Speech.Say( line, 2f );

		// Flee direction — away from the attacker with some randomness
		var awayDir = (GameObject.WorldPosition - Source.WorldPosition).WithZ( 0 ).Normal;
		var randomAngle = Game.Random.Float( -40f, 40f );
		awayDir = Rotation.FromAxis( Vector3.Up, randomAngle ) * awayDir;

		// Distance scales with panic (200–500)
		var fleeDist = 512f + 1024f * PanicLevel;
		var fleeTarget = GameObject.WorldPosition + awayDir * fleeDist;

		// Snap to navmesh
		if ( Npc.Scene.NavMesh.GetClosestPoint( fleeTarget ) is { } navPoint )
		{
			AddTask( new MoveTo( navPoint, 15f ) );
		}
		else
		{
			AddTask( new MoveTo( fleeTarget, 15f ) );
		}
	}

	protected override void OnEnd()
	{
		// Reset to normal walk speed
		// TODO: this is shit, can we scope these somehow so the IDisposable handles all this ?
		Npc.Navigation.WishSpeed = 100f;
	}

	protected override bool ShouldCancel()
	{
		return !Source.IsValid();
	}
}
