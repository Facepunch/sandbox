using Sandbox.UI;
namespace Sandbox;

public partial class SpawnlistFooter : Panel
{
	protected override int BuildHash() => HashCode.Combine( CanCreate() );

	bool CanCreate()
	{
		return true;
	}

	void CreatePopup()
	{
		var popup = new SpawnlistCreatePopup();
		popup.Parent = FindPopupPanel();
	}
}

