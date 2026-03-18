using Sandbox.UI;

/// <summary>
/// Base class for pages opened in the utility tab right panel.
/// Decorate subclasses with [Icon], [Title], [Order], and [UtilityOf(typeof(YourFunction))].
/// </summary>
public abstract class UtilityPage : Panel
{
	/// <summary>
	/// Return false to hide this page from the option list.
	/// </summary>
	public virtual bool IsVisible() => true;
}
