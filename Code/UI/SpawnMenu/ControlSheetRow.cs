using Sandbox.UI;

namespace Sandbox;

public class ControlSheetRow : Panel
{
	public SerializedProperty Property { get; set; }

	Panel _left;
	Label _title;
	Panel _right;

	public ControlSheetRow()
	{
		_left = AddChild<Panel>( "left" );
		_title = _left.AddChild<Label>( "title" );

		_right = AddChild<Panel>( "right" );
	}

	internal void Initialize( SerializedProperty prop )
	{
		_title.Text = prop.DisplayName;

		var c = BaseControl.CreateFor( prop );
		_right.AddChild( c );
	}
}
