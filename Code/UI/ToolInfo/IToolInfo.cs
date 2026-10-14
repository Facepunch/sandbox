namespace Sandbox;

public interface IToolInfo : IValid
{
	string Name { get; }
	string Description { get; }
	string PrimaryAction => null;
	string SecondaryAction => null;
	string ReloadAction => null;

	/// <summary>
	/// Label for reload and secondary held together, or null if none.
	/// </summary>
	string ReloadSecondaryAction => null;

	/// <summary>
	/// Label for holding snap, or null if none.
	/// </summary>
	string SnapAction => null;
}
