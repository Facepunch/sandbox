
namespace Sandbox.UI;

[CustomEditor( typeof( ClientInput ) )]
public partial class ClientInputControl : BaseControl
{
	DropDown _bindButton;

	public override bool SupportsMultiEdit => true;

	public ClientInputControl()
	{
		_bindButton = AddChild<DropDown>( "bind-button" );
		_bindButton.BuildOptions = BuildButtonOptions;
		_bindButton.ValueChanged = OnBindChanged;
	}

	private List<Option> BuildButtonOptions()
	{
		var options = Input.GetActions().Select( x => new Option( x.Title ?? x.Name, x.Name ) ).ToList();
		options.Insert( 0, new Option( "No Binding", "" ) );

		return options;
	}

	public override void Rebuild()
	{
		if ( Property == null ) return;

		_bindButton.Value = Property.GetValue<ClientInput>().Action;
	}

	void OnBindChanged( string value )
	{
		var current = Property.GetValue<ClientInput>();
		current.Action = value;
		Property.SetValue( current );
		
		// tony: when setting Action in current, and setting the value, the property changed event doesn't show the updated action
		// not too sure why

		foreach ( var target in Property.Parent?.Targets ?? Enumerable.Empty<object>() )
		{
			if ( target is Component component )
				GameManager.ChangeProperty( component, Property.Name, current );
		}
	}

}
