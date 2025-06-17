using Sandbox.UI;
namespace Sandbox;

public class ControlGroup : Panel
{
	public Panel Header { get; set; }
	public Panel Body { get; set; }

	public ControlGroup()
	{
		AddClass( "controlgroup" );

		Header = AddChild<Panel>( "header" );
		Body = AddChild<Panel>( "body" );
	}
}
