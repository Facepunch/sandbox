namespace Sandbox;

/// <summary>
/// Inspector panel for editing selected block properties.
/// </summary>
public class DooInspector : Widget
{
	public DooEditor Editor { get; }
	public SerializedObject Target { get; private set; }

	private Layout _content;

	public DooInspector( DooEditor editor ) : base( null )
	{
		Editor = editor;

		MinimumWidth = 300;

		Layout = Layout.Column();
		Layout.Margin = 16;
		Layout.Spacing = 4;

		_content = Layout.AddColumn();
		Layout.AddStretchCell();

		RebuildContent();
	}

	public void SetTarget( Doo.Block target ) // todo support multi-select
	{
		Target = target?.GetSerialized();
		RebuildContent();
	}

	void RebuildContent()
	{
		_content.Clear( true );

		if ( !Target.IsValid() )
			return;

		var inspector = InspectorWidget.Create( Target );
		if ( inspector == null )
			return;

		_content.Add( inspector );

		Update();
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Theme.WidgetBackground );
		Paint.DrawRect( LocalRect.Shrink( 8 ), 4 );
	}
}
