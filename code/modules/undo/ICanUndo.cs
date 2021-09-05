public interface ICanUndo
{
	/// <summary>
	/// Calls the undo method.
	/// </summary>
	void DoUndo();

	/// <summary>
	/// Checks the validity of the data in the undo block.
	/// </summary>
	/// <returns>Returns <see langword="false"/> if the content of the parameters is violated in the block, otherwise <see langword="true"/>.</returns>
	bool IsValidUndo();
}
