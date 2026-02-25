public abstract class UtilityFunction
{
	/// <summary>
	/// Execute this utility function for the given player.
	/// </summary>
	public abstract void Execute();

	/// <summary>
	/// Return false to hide this function from the menu.
	/// </summary>
	public virtual bool IsVisible() => true;
}
