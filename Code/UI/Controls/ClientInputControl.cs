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
				? $"{outputCount} Connected Output{(outputCount == 1 ? "" : "s")}"
				: "No Binding";
			SetClass( "no-binding", outputCount == 0 );
			return;
		}

		_inputHint.Action = action;
		_inputHint.SetClass( "hidden", false );
		_fallbackIcon.SetClass( "hidden", true );
		SetClass( "no-binding", false );

		var match = Input.GetActions().FirstOrDefault( a => a.Name == action );
		var label = match != null ? (match.Title ?? match.Name) : action;
		_bindLabel.Text = outputCount > 0 ? $"{label} + {outputCount}" : label;
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );

		var menu = Sandbox.MenuPanel.Open( this );
		menu.AddOption( "", "No Key Binding", () => OnBindChanged( "" ) );

		var outputs = GetLinkedOutputs().ToArray();
		if ( outputs.Length > 0 )
		{
			menu.AddSubmenu( "cable", "Linked Outputs", sub =>
			{
				foreach ( var output in outputs )
				{
					var connected = IsConnected( output );
					var source = output;
					sub.AddOption(
						connected ? "check_box" : "check_box_outline_blank",
						$"{output.Component.GameObject.Name}: {output.Title}",
						() => SetConnected( source, !connected )
					);
				}
			} );
		}

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
		var title = !string.IsNullOrEmpty( a.Title ) ? a.Title : a.Name;
		var origin = Input.GetButtonOrigin( a.Name );
		return origin != null ? $"{title} ({origin})" : title;
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
