
namespace Sandbox.UI;

[CustomEditor( typeof( ClientInput ) )]
public partial class ClientInputControl : BaseControl
{
	DropDown _bindButton;

	SerializedObject _so;

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

		Property.TryGetAsObject( out _so );

		_bindButton.Value = _so.GetProperty( nameof( ClientInput.Action ) ).GetValue<string>();
	}

	void OnBindChanged( string value )
	{
		if ( _so == null ) return;
		_so.GetProperty( nameof( ClientInput.Action ) ).SetValue( value );
	}

}
