public sealed partial class Player
{
	/// <summary>
	/// Access the undo system for this player
	/// </summary>
	public UndoSystem.PlayerStack Undo => UndoSystem.Current.For( SteamId );
}
