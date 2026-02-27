namespace Sandbox;

public interface IToolInfo : IValid
{
	string Name { get; }
	string Description { get; }
	string PrimaryAction => null;
	string SecondaryAction => null;
	string ReloadAction => null;
}
