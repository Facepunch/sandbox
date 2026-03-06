namespace Sandbox;

/// <summary>
/// Control widget for editing Doo properties in the inspector.
/// Shows an "Edit" button that opens the Doo editor.
/// </summary>
[CustomEditor( typeof( Doo ) )]
public class DooControlWidget : ControlWidget
{
	public DooControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Row();
		Layout.Spacing = 4;
		Layout.Margin = 2;

		Layout.AddStretchCell();

		var clearBtn = Layout.Add( new IconButton( "clear" ) );
		clearBtn.ToolTip = "Clear";
		clearBtn.OnClick = OnClearClicked;
		clearBtn.MaximumHeight = Theme.RowHeight - 4;
		clearBtn.FixedHeight = clearBtn.MaximumHeight;

		Cursor = CursorShape.Finger;
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		OnEditClicked();
	}

	void OnEditClicked()
	{
		var doo = SerializedProperty.GetValue<Doo>();

		if ( doo == null )
		{
			doo = new Doo();
			SerializedProperty.SetValue( doo );
		}

		SerializedProperty.TryGetAsObject( out var so );

		var title = SerializedProperty.Name ?? "Doo";
		var editor = DooEditor.Open( so, title );

	}

	void OnClearClicked()
	{
		SerializedProperty.SetValue<Doo>( null );
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		var doo = SerializedProperty.GetValue<Doo>();
		if ( doo is null ) return;

		Paint.Pen = Theme.TextLight;
		Paint.DrawText( LocalRect.Shrink( 8, 4 ), $"{doo.GetLabel()}", TextFlag.LeftCenter );

		// Don't paint default background
	}
}
