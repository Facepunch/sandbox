using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// Engages a visible player: close the gap, fire a burst, pause, then reposition to a flanking point.
/// Cancels immediately if the target leaves sight.
/// </summary>
public class CombatEngageSchedule : ScheduleBase
{
	private static readonly string[] SpotLines =
	{
		"Contact!",
		"There!",
		"I see you!",
		"Got one!",
		"Enemy spotted!",
		"Don't move!",
		"Found you.",
	};

	private static readonly string[] TauntLines =
	{
		"You're not getting away!",
		"Stay down!",
		"Take cover!",
		"Suppressing fire!",
		"Keep the pressure on!",
		"Don't let up!",
		"That's for my squad!",
		"You picked the wrong fight.",
	};

	/// <summary>
	/// The player to engage.
	/// </summary>
	public GameObject Target { get; set; }

	/// <summary>
	/// Weapon to fire. Should be a child component on the NPC's GameObject.
	/// </summary>
	public BaseWeapon Weapon { get; set; }

	/// <summary>
	/// Distance at which the NPC stops advancing and begins shooting.
	/// </summary>
	public float AttackRange { get; set; } = 300f;

	/// <summary>
	/// How long each shooting burst lasts.
	/// </summary>
	public float BurstDuration { get; set; } = 1.5f;

	/// <summary>
	/// Pause between burst end and repositioning.
	/// </summary>
	public float BurstPause { get; set; } = 0.8f;

	/// <summary>
	/// Speed the NPC moves when engaging.
	/// </summary>
	public float EngageSpeed { get; set; } = 180f;

	/// <summary>
	/// Radius around the current position to pick a flanking point.
	/// </summary>
	public float FlankRadius { get; set; } = 250f;

	protected override void OnStart()
	{
		Npc.Navigation.WishSpeed = EngageSpeed;

		// Set look target now so the NPC tracks the player through all tasks,
		// movement, firing, waiting, and repositioning.
		Npc.Animation.SetLookTarget( Target );

		// Spot the target on engage start
		if ( Npc.Speech.CanSpeak )
			AddTask( new Say( Game.Random.FromArray( SpotLines ), 1.5f ) );

		AddTask( new LookAt( Target ) );
		AddTask( new MoveTo( Target, AttackRange ) );
		AddTask( new FireWeapon( Weapon, Target, BurstDuration ) );

		// Random combat taunt during the pause after firing
		if ( Npc.Speech.CanSpeak && Game.Random.Float() < 0.4f )
			AddTask( new Say( Game.Random.FromArray( TauntLines ), BurstPause ) );
		else
			AddTask( new Wait( BurstPause ) );

		AddTask( new MoveTo( GetFlankPosition(), 20f ) );
	}

	protected override void OnEnd()
	{
		Npc.Navigation.WishSpeed = 100f;
		Npc.Animation.ClearLookTarget();
	}

	protected override bool ShouldCancel()
	{
		if ( !Target.IsValid() )
			return true;

		return !Npc.Senses.VisibleTargets.Contains( Target );
	}

	/// <summary>
	/// Pick a random position perpendicular to the NPC→target axis at <see cref="FlankRadius"/>.
	/// Snaps to navmesh if possible.
	/// </summary>
	private Vector3 GetFlankPosition()
	{
		Vector3 toTarget = Target.IsValid()
			? (Target.WorldPosition - Npc.WorldPosition).WithZ( 0 ).Normal
			: Npc.WorldRotation.Forward;

		// Perpendicular + slight forward bias, randomized left/right
		var perp = new Vector3( -toTarget.y, toTarget.x, 0 );
		var side = Game.Random.Float() > 0.5f ? 1f : -1f;
		var flankDir = (perp * side + toTarget * 0.3f).WithZ( 0 ).Normal;
		var candidate = Npc.WorldPosition + flankDir * Game.Random.Float( FlankRadius * 0.5f, FlankRadius );

		if ( Npc.Scene.NavMesh.GetClosestPoint( candidate ) is { } nav )
			return nav;

		return candidate;
	}
}
