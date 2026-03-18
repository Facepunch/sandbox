using Sandbox.UI;

public abstract class UtilityFunction
{
	/// <summary>
	/// Execute this utility function directly.
	/// Called when no <see cref="UtilityPage"/> types are registered for this function.
	/// </summary>
	public virtual void Execute() { }

	/// <summary>
	/// Return false to hide this function from the menu.
	/// </summary>
	public virtual bool IsVisible() => true;
}
