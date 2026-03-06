namespace Sandbox;

public class DooVariableControlWidget : StringControlWidget
{
	public DooVariableControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Row();
		Layout.Add( LineEdit );
		Layout.Add( new IconButton( "arrow_drop_down" ) { Background = Color.Transparent, OnClick = OpenMenu } );
	}

	void OpenMenu()
	{
		var editor = GetAncestor<DooEditor>();

		var menu = new ContextMenu( this );

		if ( editor.ArgumentHints != null )
		{
			foreach ( var arg in editor.ArgumentHints )
			{
				menu.AddOption( $"{arg.Name} ({arg.Hint.Name})", "", () => { SerializedProperty.SetValue( arg.Name ); } );
			}
		}

		foreach ( var arg in editor.GetArguments() )
		{
			if ( editor.ArgumentHints?.Any( x => x.Name == arg ) == true )
				continue;

			menu.AddOption( $"{arg}", "", () => { SerializedProperty.SetValue( arg ); } );
		}

		menu.OpenNextTo( this, WidgetAnchor.BottomStart );
	}
}
