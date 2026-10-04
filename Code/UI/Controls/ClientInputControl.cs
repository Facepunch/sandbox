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
		var outputCount = GetLinkedOutputs().Count( IsConnected );

		if ( string.IsNullOrWhiteSpace( action ) )
		{
			_inputHint.Action = null;
			_inputHint.SetClass( "hidden", true );
			_fallbackIcon.SetClass( "hidden", false );
			_bindLabel.Text = outputCount > 0
				? Game.Language.GetPhrase( outputCount == 1 ? "ui.input.one_connected_output" : "ui.input.connected_outputs", new() { { "count", outputCount } } )
				: "#ui.input.no_binding";
			SetClass( "no-binding", outputCount == 0 );
			return;
		}

		_inputHint.Action = action;
		_inputHint.SetClass( "hidden", false );
		_fallbackIcon.SetClass( "hidden", true );
		SetClass( "no-binding", false );

		var match = Input.GetActions().FirstOrDefault( a => a.Name == action );
		var label = match != null ? LocalizedText.InputTitle( match ) : action;
		_bindLabel.Text = outputCount > 0
			? Game.Language.GetPhrase( "ui.input.binding_with_outputs", new() { { "binding", label }, { "count", outputCount } } )
			: label;
	}

	/// <summary>
	/// Refreshes composed binding and connection labels when the language changes.
	/// </summary>
	public override void LanguageChanged()
	{
		base.LanguageChanged();
		Rebuild();
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );

		var menu = new Sandbox.UI.Menu();
		menu.AddOption( "#ui.input.no_key_binding", "", () => OnBindChanged( "" ) );

		var outputs = GetLinkedOutputs().ToArray();
		if ( outputs.Length > 0 )
		{
			var sub = menu.AddMenu( "#ui.input.linked_outputs", "cable" );
			foreach ( var output in outputs )
			{
				var connected = IsConnected( output );
				var source = output;
				sub.AddOption(
					Game.Language.GetPhrase( "ui.input.linked_output", new()
					{
						{ "object", LocalizedText.Resolve( output.Component.GameObject.Name ) },
						{ "output", LocalizedText.Resolve( output.Title ) }
					} ),
					connected ? "check_box" : "check_box_outline_blank",
					() => SetConnected( source, !connected )
				);
			}
		}

		menu.AddSeparator();

		var grouped = Input.GetActions()
			.GroupBy( a => a.GroupName ?? "" )
			.OrderBy( g => LocalizedText.InputGroup( g.Key ), StringComparer.CurrentCultureIgnoreCase );

		foreach ( var group in grouped )
		{
			if ( string.IsNullOrWhiteSpace( group.Key ) )
			{
				foreach ( var action in group )
				{
					var a = action;
					menu.AddOption( ActionLabel( a ), "", () => OnBindChanged( a.Name ) );
				}
			}
			else
			{
				var groupActions = group.ToList();
				var sub = menu.AddMenu( LocalizedText.InputGroup( group.Key ), "" );
				foreach ( var action in groupActions )
				{
					var a = action;
					sub.AddOption( ActionLabel( a ), "", () => OnBindChanged( a.Name ) );
				}
			}
		}

		menu.Open( this, Popup.PositionMode.UnderMouse );
	}

	IEnumerable<SignalOutputDescription> GetLinkedOutputs()
	{
		var visitedRoots = new HashSet<GameObject>();

		foreach ( var target in GetTargetComponents() )
		{
			if ( !visitedRoots.Add( target.GameObject.Root ) ) continue;

			foreach ( var output in SignalSystem.GetContraptionOutputs( target.GameObject ) )
			{
				if ( CanConnect( output ) ) yield return output;
			}
		}
	}

	bool CanConnect( SignalOutputDescription output )
	{
		return GetTargetInputs().Any( input => SignalSystem.AreCompatible( output, input ) );
	}

	IEnumerable<SignalInputDescription> GetTargetInputs()
	{
		foreach ( var target in GetTargetComponents() )
		{
			foreach ( var input in SignalSystem.GetInputs( target ) )
			{
				if ( string.Equals( input.Id, Property.Name, StringComparison.OrdinalIgnoreCase ) )
					yield return input;
			}
		}
	}

	IEnumerable<Component> GetTargetComponents()
	{
		foreach ( var target in Property.Parent?.Targets ?? Enumerable.Empty<object>() )
		{
			if ( target is Component component && component.IsValid() )
				yield return component;
		}
	}

	bool IsConnected( SignalOutputDescription output )
	{
		return GetTargetInputs().Any( input => SignalSystem.IsConnected( output, input ) );
	}

	void SetConnected( SignalOutputDescription output, bool connected )
	{
		foreach ( var input in GetTargetInputs() )
			SignalSystem.SetConnected( output, input, connected );

		Rebuild();
	}

	string ActionLabel( InputAction a )
	{
		var title = LocalizedText.InputTitle( a );
		var origin = Input.GetButtonOrigin( a.Name );
		return origin != null
			? Game.Language.GetPhrase( "ui.input.binding_with_key", new() { { "binding", title }, { "key", origin } } )
			: title;
	}

	void OnBindChanged( string value )
	{
		var current = Property.GetValue<ClientInput>();
		current.Action = value;
		Property.SetValue( current );

		foreach ( var target in Property.Parent?.Targets ?? Enumerable.Empty<object>() )
		{
			if ( target is Component component )
				GameManager.ChangeProperty( component, Property.Name, current );
		}

		Rebuild();
	}
}
