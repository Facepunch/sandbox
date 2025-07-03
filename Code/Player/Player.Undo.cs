public sealed partial class Player
{
	/// <summary>
	/// Allows adding to a list of actions that can be undone.
	/// </summary>
	public UndoSystem Undo { get; private set; }
}
