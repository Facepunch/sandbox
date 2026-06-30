namespace Sandbox.Npcs;

/// <summary>
/// What an NPC is aware of on a given tick -- a mix of things it perceives (a visible
/// hostile) and things it knows about itself (recently damaged, afraid). Schedules declare
/// which of these interrupt them via <see cref="ScheduleBase.InterruptedBy"/>. Extend in
/// <see cref="Npc.GatherAwareness"/> on a subclass to add NPC-specific awareness.
/// </summary>
[Flags]
public enum NpcAwareness
{
	None = 0,

	/// <summary>A hostile target is currently visible.</summary>
	SeesHostile = 1 << 0,

	/// <summary>A hostile target is currently within hearing range.</summary>
	HearsHostile = 1 << 1,

	/// <summary>A hostile target is inside the NPC's personal space.</summary>
	HostileInPersonalSpace = 1 << 2,

	/// <summary>A visible entity the NPC is afraid of -- something it wants to flee.</summary>
	SeesThreat = 1 << 6,

	/// <summary>The NPC took damage recently.</summary>
	Damaged = 1 << 3,

	/// <summary>The NPC is afraid (subclass-driven).</summary>
	Afraid = 1 << 4,

	/// <summary>The NPC's health is low (subclass-driven).</summary>
	LowHealth = 1 << 5,
}
