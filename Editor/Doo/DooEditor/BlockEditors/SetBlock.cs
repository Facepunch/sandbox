namespace Sandbox;

/// <summary>
/// Main window for editing Doo scripts.
/// </summary>
[Inspector( typeof( Doo.SetBlock ) )]
public class SetBlock : InspectorWidget
{
	SerializedObject Target { get; }

	SerializedProperty _nameProperty;
	SerializedProperty _valueProperty;

	public SetBlock( SerializedObject obj ) : base( obj )
	{
		Target = obj;
		Layout = Layout.Column();
		Layout.Spacing = 4;

		_nameProperty = Target.GetProperty( nameof( Doo.SetBlock.VariableName ) );
		_valueProperty = Target.GetProperty( nameof( Doo.SetBlock.Value ) );

		BuildUI();
	}

	void BuildUI()
	{
		Layout.Clear( true );

		var cs = new ControlSheet();
		cs.AddControl<DooVariableControlWidget>( _nameProperty );
		cs.AddRow( _valueProperty );

		Layout.Add( cs );
	}
}
