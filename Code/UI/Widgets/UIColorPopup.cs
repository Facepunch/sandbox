using Sandbox.UI;

namespace Sandbox;

public class UIColorPopup : Popup
{
	public UIColorPopup( Panel source, SerializedProperty property )
		: base( source, PositionMode.BelowLeft, 4f )
	{
		var picker = AddChild<ColorPickerControl>();
		picker.Property = property;

		// force rename "Bright" to "Brightness" and to let it be localized (hacky?)
		foreach ( var label in picker.Descendants.OfType<Label>() )
		{
			if ( label.Text == "Bright" ) label.Text = "#ui.color.brightness";
		}
	}
}
