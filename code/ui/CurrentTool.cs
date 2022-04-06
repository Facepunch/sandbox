using Sandbox;
using Sandbox.Tools;
using Sandbox.UI;
using Sandbox.UI.Construct;

public class CurrentTool : Panel
{
	public Label Title;
	public Label Description;

	public CurrentTool()
	{
		Title = Add.Label( "Tool", "title" );
		Description = Add.Label( "This is a tool", "description" );
	}

	public override void Tick()
	{
		var tool = GetCurrentTool();
		SetClass( "active", tool is not null );

		if ( tool is not null )
		{
			Title.SetText( tool.ClassInfo.Title );
			Description.SetText( tool.ClassInfo.Description );
		}
	}

	BaseTool GetCurrentTool()
	{
		var player = Local.Pawn as Player;
		if ( player is null ) return null;

		var inventory = player.Inventory;
		if ( inventory is null ) return null;

		if ( inventory.Active is not Tool tool ) return null;

		return tool?.CurrentTool;
	}
}
