namespace Sandbox.AI;

/// <summary>
/// Basic relationship information for NPCs
/// </summary>
public enum Relationship
{
	Neutral,
	Friendly,
	Hostile
}

/// <summary>
/// A finite amount of states for a NPC. We could expand on this later.
/// </summary>
public enum State
{
	Idle,
	Move,
	Attack,
	Flee,
	Follow,
	KeepDistance
}
