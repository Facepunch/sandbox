
namespace Sandbox.UI;

[CustomEditor( typeof( ClientInput ) )]
public partial class ClientInputControl : BaseControl
{
	Panel _preview;
	InputHint _inputHint;
	IconPanel _fallbackIcon;
	Label _bindLabel;

	public override bool SupportsMultiEdit => true;

	public ClientInputControl()
	{
		_preview = AddChild<Panel>( "preview" );
		_inputHint = _preview.AddChild<InputHint>( "hint" );
		_fallbackIcon = _preview.AddChild<IconPanel>( "fallback" );
		_fallbackIcon.Text = "keyboard";

		_bindLabel = AddChild<Label>( "bind-label" );
	}

	public override void Rebuild()
	{
		if ( Property == null ) return;

		var action = Property.GetValue<ClientInput>().Action;

		if ( string.IsNullOrWhiteSpace( action ) )
		{
			_inputHint.Action = null;
			_inputHint.SetClass( "hidden", true );
			_fallbackIcon.SetClass( "hidden", false );
			_bindLabel.Text = "No Binding";
			SetClass( "no-binding", true );
			return;
		}

		_inputHint.Action = action;
		_inputHint.SetClass( "hidden", false );
		_fallbackIcon.SetClass( "hidden", true );
		SetClass( "no-binding", false );

		var match = Input.GetActions().FirstOrDefault( a => a.Name == action );
		_bindLabel.Text = match != null ? (match.Title ?? match.Name) : action;
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );

		var menu = Sandbox.MenuPanel.Open( this );

		menu.AddOption( "", "No Binding", () => OnBindChanged( "" ) );
		menu.AddSpacer();

		var grouped = Input.GetActions()
			.GroupBy( a => a.GroupName ?? "" )
			.OrderBy( g => g.Key );

		foreach ( var group in grouped )
		{
			if ( string.IsNullOrWhiteSpace( group.Key ) )
			{
				foreach ( var action in group )
				{
					var a = action;
					menu.AddOption( "", ActionLabel( a ), () => OnBindChanged( a.Name ) );
				}
			}
			else
			{
				var groupActions = group.ToList();
				menu.AddSubmenu( "", group.Key, sub =>
				{
					foreach ( var action in groupActions )
					{
						var a = action;
						sub.AddOption( "", ActionLabel( a ), () => OnBindChanged( a.Name ) );
					}
				} );
			}
		}
	}

	string ActionLabel( InputAction a )
	{
		var title = !string.IsNullOrEmpty( a.Title ) ? a.Title : a.Name;
		var origin = Input.GetButtonOrigin( a.Name );
		return origin != null ? $"{title} ({origin})" : title;
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

		Rebuild();
	}
}
