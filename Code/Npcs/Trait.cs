namespace Sandbox.Npcs;

/// <summary>
/// The shared vocabulary NPCs use to describe themselves and react to one another. An NPC
/// advertises traits (what it is) via <see cref="Npc.Traits"/> and reacts to other entities'
/// traits in <see cref="Npc.GetDispositionTo"/>. These are just plain strings -- this list is
/// the common vocabulary so built-in and UGC NPCs interoperate, but any NPC is free to invent
/// its own traits and react to them.
/// </summary>
public static class Trait
{
	/// <summary>A player.</summary>
	public const string Player = "player";

	/// <summary>A living creature -- made of flesh, can be hurt.</summary>
	public const string Living = "living";

	/// <summary>Dangerous to bystanders -- armed, aggressive, a monster. Civilians flee these.</summary>
	public const string Threat = "threat";

	/// <summary>A non-combatant.</summary>
	public const string Civilian = "civilian";

	/// <summary>A hunting animal. Prey flees these.</summary>
	public const string Predator = "predator";

	/// <summary>A hunted animal.</summary>
	public const string Prey = "prey";
}
