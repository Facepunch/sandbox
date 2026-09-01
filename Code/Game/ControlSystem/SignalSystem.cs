/// <summary>
/// The raw value of a signal delivery. You rarely need this — declare inputs as plain
/// <c>bool</c>, <c>float</c>, or parameterless methods and the system converts for you.
/// </summary>
public readonly record struct SignalEvent( float Analog, bool Down, bool Pressed, bool Released, Player Instigator );

/// <summary>
/// Makes a method or property wireable as an input. Works on: a parameterless method (fires
/// once when the signal turns on), a method taking a <c>bool</c> or <c>float</c>, or a writable
/// <c>bool</c>/<c>float</c> property.
/// The member's name is the wire's name — renaming it breaks saved contraptions.
/// </summary>
[AttributeUsage( AttributeTargets.Method | AttributeTargets.Property )]
public sealed class SignalInputAttribute : Attribute
{
	/// <summary>
	/// The stable port id. Defaults to the member name.
	/// </summary>
	public string Id { get; set; }
	public string Name { get; set; }
	public bool Default { get; set; }
}

/// <summary>
/// Makes a <see cref="SignalOutput"/> property wireable as an output.
/// The member's name is the wire's name — renaming it breaks saved contraptions.
/// </summary>
[AttributeUsage( AttributeTargets.Property )]
public sealed class SignalOutputAttribute : Attribute
{
	public string Name { get; set; }
	public bool Default { get; set; }
}

/// <summary>
/// One wire: which component and which input it feeds into.
/// </summary>
public sealed class SignalConnection
{
	[Property]
	public Component Target { get; set; }

	[Property]
	public string Input { get; set; }
}

/// <summary>
/// An output port that players can wire to inputs on other entities. Call whichever
/// <c>Emit</c> overload matches what you have — the receiving end converts as needed.
/// State providers should emit every tick while active; this keeps continuous inputs push-driven.
/// Only does anything on the host.
///
/// Credit follows automatically: whoever last pressed or signaled this component is
/// passed along with the emit. Pass an instigator explicitly only to override that.
/// </summary>
public sealed class SignalOutput
{
	[Property]
	public List<SignalConnection> Connections { get; set; } = new();

	private bool _lastDown;
	private float _lastAnalog;

	/// <summary>
	/// Fire a one-shot pulse — "something happened once".
	/// </summary>
	public void Emit( Component source, Player instigator = null )
	{
		instigator ??= SignalSystem.InstigatorFor( source );
		SignalSystem.Emit( this, new SignalEvent( 1f, true, true, false, instigator ) );
		SignalSystem.Emit( this, new SignalEvent( 0f, false, false, true, instigator ) );
		_lastDown = false;
		_lastAnalog = 0f;
	}

	/// <summary>
	/// Emit an on/off state.
	/// </summary>
	public void Emit( Component source, bool value, Player instigator = null )
	{
		// Active state is continuous; idle only needs to travel once on release.
		if ( !value && !_lastDown ) return;

		var pressed = value && !_lastDown;
		var released = !value && _lastDown;
		_lastDown = value;
		_lastAnalog = value ? 1f : 0f;

		instigator ??= SignalSystem.InstigatorFor( source );
		SignalSystem.Emit( this, new SignalEvent( _lastAnalog, value, pressed, released, instigator ) );
	}

	/// <summary>
	/// Emit a number. Values above 0.5 count as "on".
	/// </summary>
	public void Emit( Component source, float value, Player instigator = null )
	{
		// Keep non-zero analog signals continuous without ticking dormant graphs.
		if ( value.AlmostEqual( 0f ) && _lastAnalog.AlmostEqual( 0f ) ) return;

		var down = value > 0.5f;
		var pressed = down && !_lastDown;
		var released = !down && _lastDown;
		_lastDown = down;
		_lastAnalog = value;

		instigator ??= SignalSystem.InstigatorFor( source );
		SignalSystem.Emit( this, new SignalEvent( value, down, pressed, released, instigator ) );
	}
}

internal readonly record struct SignalInputContext( PropertyDescription Property, SignalInputAttribute Attribute );

/// <summary>
/// What actually runs when a signal arrives at an input.
/// </summary>
internal readonly record struct SignalInputBinding(
	MethodDescription Method,
	PropertyDescription Property,
	SignalInputKind Kind,
	Action<Component, SignalEvent> Custom = null )
{
	public void Invoke( Component component, SignalEvent value )
	{
		switch ( Kind )
		{
			case SignalInputKind.TriggerMethod:
				if ( value.Pressed ) Method.Invoke( component, null );
				break;
			case SignalInputKind.EventMethod:
				Method.Invoke( component, [value] );
				break;
			case SignalInputKind.BoolMethod:
				Method.Invoke( component, [value.Down] );
				break;
			case SignalInputKind.FloatMethod:
				Method.Invoke( component, [value.Analog] );
				break;
			case SignalInputKind.BoolProperty:
				Property.SetValue( component, value.Down );
				break;
			case SignalInputKind.FloatProperty:
				Property.SetValue( component, value.Analog );
				break;
			case SignalInputKind.Custom:
				Custom?.Invoke( component, value );
				break;
		}
	}
}

internal enum SignalInputKind
{
	TriggerMethod,
	EventMethod,
	BoolMethod,
	FloatMethod,
	BoolProperty,
	FloatProperty,
	Custom
}

/// <summary>
/// Lets a custom property type act as a signal input. Just implement this anywhere —
/// it's found automatically.
/// </summary>
internal interface ISignalAdapter
{
	Type TargetType { get; }
	bool TryBind( SignalInputContext context, out SignalInputBinding binding );
}

/// <summary>
/// An output port found on a component: its name and how to reach it.
/// </summary>
internal sealed class SignalOutputDescription
{
	public Component Component { get; }
	public string Id { get; }
	public string Title { get; }
	public bool IsDefault { get; }
	public SignalOutput Port { get; }

	internal SignalOutputDescription( Component component, SignalPort port, SignalOutput output )
	{
		Component = component;
		Id = port.Id;
		Title = port.Title;
		IsDefault = port.IsDefault;
		Port = output;
	}
}

/// <summary>
/// An input port found on a component: its name and how to reach it.
/// </summary>
internal sealed class SignalInputDescription
{
	public Component Component { get; }
	public string Id { get; }
	public string Title { get; }
	public bool IsDefault { get; }

	internal SignalInputDescription( Component component, SignalPort port )
	{
		Component = component;
		Id = port.Id;
		Title = port.Title;
		IsDefault = port.IsDefault;
	}
}

/// <summary>
/// One input or output declared on a component type.
/// </summary>
internal readonly record struct SignalPort( string Id, string Title, bool IsDefault )
{
	/// <summary>
	/// Outputs: the property holding the SignalOutput.
	/// </summary>
	public PropertyDescription Property { get; init; }

	/// <summary>
	/// Inputs: what to run when a signal arrives.
	/// </summary>
	public SignalInputBinding Binding { get; init; }
}

/// <summary>
/// Delivers signals from outputs to the inputs they're wired to. Everything runs on the host;
/// clients just see the results through normal networking. TypeLibrary owns member caching and
/// hotload invalidation; this system resolves ports from its current descriptions when needed.
/// </summary>
internal static class SignalSystem
{
	// Who last pressed or signaled each component, so gates that re-emit later still
	// pass the credit along without hand-carrying it. Pruned as components die.
	private static readonly Dictionary<Component, Player> _instigators = new();

	/// <summary>
	/// The player who last pressed or signaled this component, or null.
	/// </summary>
	internal static Player InstigatorFor( Component component )
	{
		if ( component.IsValid() && _instigators.TryGetValue( component, out var player ) && player.IsValid() )
			return player;

		return null;
	}

	private static void RememberInstigator( Component component, Player player )
	{
		if ( !component.IsValid() || !player.IsValid() ) return;

		// Idk
		if ( _instigators.Count > 256 )
		{
			foreach ( var dead in _instigators.Keys.Where( key => !key.IsValid() ).ToArray() )
				_instigators.Remove( dead );
		}

		_instigators[component] = player;
	}

	public static IEnumerable<SignalOutputDescription> GetOutputs( Component component )
	{
		if ( !component.IsValid() ) yield break;

		foreach ( var port in GetOutputPorts( component ) )
		{
			if ( port.Property.GetValue( component ) is SignalOutput output )
				yield return new SignalOutputDescription( component, port, output );
		}
	}

	public static IEnumerable<SignalInputDescription> GetInputs( Component component )
	{
		if ( !component.IsValid() ) yield break;

		foreach ( var port in GetInputPorts( component ) )
			yield return new SignalInputDescription( component, port );
	}

	/// <summary>
	/// Any output can wire to any input
	/// </summary>
	public static bool AreCompatible( SignalOutputDescription output, SignalInputDescription input )
	{
		return output is not null && input is not null;
	}

	public static bool IsConnected( SignalOutputDescription output, SignalInputDescription input )
	{
		if ( output?.Port?.Connections is null || input is null ) return false;
		return output.Port.Connections.Any( connection => connection?.Target == input.Component && connection.Input == input.Id );
	}

	public static void SetConnected( SignalOutputDescription output, SignalInputDescription input, bool connected )
	{
		if ( output is null || input is null ) return;
		SetConnectionRpc( output.Component, output.Id, input.Component, input.Id, connected );
	}

	[Rpc.Host]
	private static void SetConnectionRpc( Component source, string output, Component target, string input, bool connected )
	{
		if ( !source.IsValid() || !target.IsValid() ) return;

		// Prop protection, and wires can't leave the contraption.
		if ( !source.GameObject.HasAccess( Rpc.Caller ) ) return;

		var linked = new LinkedGameObjectBuilder();
		linked.AddConnected( source.GameObject );
		if ( !linked.Objects.Contains( target.GameObject.Root ) ) return;

		SetConnectedInternal( source, output, target, input, connected );
	}

	private static bool SetConnectedInternal( Component source, string outputId, Component target, string inputId, bool connected )
	{
		if ( !TryFindOutput( source, outputId, out var output ) ) return false;
		if ( !TryFindInput( target, inputId, out var input ) ) return false;
		if ( output.Property.GetValue( source ) is not SignalOutput port ) return false;

		var connections = port.Connections ??= new();
		var wasConnected = connections.Any( connection => connection?.Target == target && connection.Input == inputId );
		connections.RemoveAll( connection => connection is null
			|| !connection.Target.IsValid()
			|| (connection.Target == target && connection.Input == inputId) );

		if ( connected )
			connections.Add( new SignalConnection { Target = target, Input = inputId } );
		else if ( wasConnected )
			Deliver( target, input, new SignalEvent( 0f, false, false, true, null ) ); // let go of anything held on

		source.GameObject.Network?.Refresh();
		return true;
	}

	internal static void Emit( SignalOutput port, SignalEvent value )
	{
		if ( !Networking.IsHost || port?.Connections is null ) return;

		foreach ( var connection in port.Connections )
		{
			if ( connection?.Target.IsValid() != true ) continue;

			if ( !TryFindInput( connection.Target, connection.Input, out var input ) ) continue;

			Deliver( connection.Target, input, value );
		}
	}

	/// <summary>
	/// Every output on this object's contraption (anything joined by welds, joints or links).
	/// Tools and UI should use this so they all agree on what's wireable.
	/// </summary>
	public static IEnumerable<SignalOutputDescription> GetContraptionOutputs( GameObject gameObject )
	{
		return GetContraptionComponents( gameObject ).SelectMany( GetOutputs );
	}

	/// <summary>
	/// Every input on this object's contraption.
	/// </summary>
	public static IEnumerable<SignalInputDescription> GetContraptionInputs( GameObject gameObject )
	{
		return GetContraptionComponents( gameObject ).SelectMany( GetInputs );
	}

	private static IEnumerable<Component> GetContraptionComponents( GameObject gameObject )
	{
		if ( !gameObject.IsValid() ) yield break;

		var linked = new LinkedGameObjectBuilder();
		linked.AddConnected( gameObject );

		foreach ( var root in linked.Objects )
		{
			foreach ( var component in root.GetComponentsInChildren<Component>( true ) )
			{
				if ( component.IsValid() ) yield return component;
			}
		}
	}

	/// <summary>
	/// Wire two objects together automatically — used by the Linker tool. Connects when there's
	/// one obvious pairing (both marked Default, or only one option); does nothing when ambiguous.
	/// </summary>
	public static bool TryAutoConnect( GameObject first, GameObject second )
	{
		if ( !first.IsValid() || !second.IsValid() ) return false;

		var candidates = FindCandidates( first, second ).Concat( FindCandidates( second, first ) ).ToArray();
		var preferred = candidates.Where( candidate => candidate.Output.IsDefault && candidate.Input.IsDefault ).ToArray();
		var selected = preferred.Length == 1 ? preferred[0] : candidates.Length == 1 ? candidates[0] : default;
		if ( selected.Output is null ) return false;

		return SetConnectedInternal( selected.Output.Component, selected.Output.Id, selected.Input.Component, selected.Input.Id, true );
	}

	private static IEnumerable<(SignalOutputDescription Output, SignalInputDescription Input)> FindCandidates( GameObject source, GameObject target )
	{
		var inputs = target.Root.GetComponentsInChildren<Component>( true ).SelectMany( GetInputs ).ToArray();

		foreach ( var output in source.Root.GetComponentsInChildren<Component>( true ).SelectMany( GetOutputs ) )
		{
			foreach ( var input in inputs )
				yield return (output, input);
		}
	}

	private static void Deliver( Component component, SignalPort input, SignalEvent value )
	{
		RememberInstigator( component, value.Instigator );

		try
		{
			using var scope = ControlContext.Push( value.Instigator );
			input.Binding.Invoke( component, value );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Signal input {component}.{input.Id} failed: {exception.Message}" );
		}
	}

	private static IEnumerable<SignalPort> GetOutputPorts( Component component )
	{
		var type = Game.TypeLibrary.GetType( component.GetType() );
		if ( type is null ) yield break;

		var ids = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var property in type.Properties )
		{
			var attribute = property.GetCustomAttribute<SignalOutputAttribute>();
			if ( attribute is null ) continue;
			if ( !TryCreateOutput( type, property, attribute, out var port ) ) continue;

			if ( !ids.Add( port.Id ) )
			{
				Log.Warning( $"Signal output '{port.Id}' is declared twice on {type.FullName}; the duplicate was ignored." );
				continue;
			}

			yield return port;
		}
	}

	private static IEnumerable<SignalPort> GetInputPorts( Component component )
	{
		var type = Game.TypeLibrary.GetType( component.GetType() );
		if ( type is null ) yield break;

		var ids = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var method in type.Methods )
		{
			var attribute = method.GetCustomAttribute<SignalInputAttribute>();
			if ( attribute is null ) continue;
			if ( !TryCreateInput( type, method, attribute, out var port ) ) continue;
			if ( TryAddInput( type, ids, port ) ) yield return port;
		}

		foreach ( var property in type.Properties )
		{
			var attribute = property.GetCustomAttribute<SignalInputAttribute>();
			if ( attribute is null ) continue;
			if ( !TryCreateInput( type, property, attribute, out var port ) ) continue;
			if ( TryAddInput( type, ids, port ) ) yield return port;
		}
	}

	private static bool TryAddInput( TypeDescription type, HashSet<string> ids, SignalPort port )
	{
		if ( ids.Add( port.Id ) ) return true;

		Log.Warning( $"Signal input '{port.Id}' is declared twice on {type.FullName}; the duplicate was ignored." );
		return false;
	}

	private static bool TryFindOutput( Component component, string id, out SignalPort result )
	{
		result = default;
		var found = false;
		var type = Game.TypeLibrary.GetType( component.GetType() );
		if ( type is null ) return false;

		foreach ( var property in type.Properties )
		{
			var attribute = property.GetCustomAttribute<SignalOutputAttribute>();
			if ( attribute is null || !string.Equals( property.Name, id, StringComparison.OrdinalIgnoreCase ) ) continue;
			if ( !TryCreateOutput( type, property, attribute, out var port ) ) continue;
			if ( found )
			{
				Log.Warning( $"Signal output '{id}' is declared twice on {type.FullName}; refusing to choose one." );
				return false;
			}

			result = port;
			found = true;
		}

		return found;
	}

	private static bool TryFindInput( Component component, string id, out SignalPort result )
	{
		result = default;
		var found = false;
		var type = Game.TypeLibrary.GetType( component.GetType() );
		if ( type is null ) return false;

		foreach ( var method in type.Methods )
		{
			var attribute = method.GetCustomAttribute<SignalInputAttribute>();
			var portId = attribute?.Id ?? method.Name;
			if ( attribute is null || !string.Equals( portId, id, StringComparison.OrdinalIgnoreCase ) ) continue;
			if ( !TryCreateInput( type, method, attribute, out var port ) ) continue;
			if ( found ) return WarnDuplicateInput( type, id, out result );

			result = port;
			found = true;
		}

		foreach ( var property in type.Properties )
		{
			var attribute = property.GetCustomAttribute<SignalInputAttribute>();
			var portId = attribute?.Id ?? property.Name;
			if ( attribute is null || !string.Equals( portId, id, StringComparison.OrdinalIgnoreCase ) ) continue;
			if ( !TryCreateInput( type, property, attribute, out var port ) ) continue;
			if ( found ) return WarnDuplicateInput( type, id, out result );

			result = port;
			found = true;
		}

		return found;
	}

	private static bool WarnDuplicateInput( TypeDescription type, string id, out SignalPort result )
	{
		result = default;
		Log.Warning( $"Signal input '{id}' is declared twice on {type.FullName}; refusing to choose one." );
		return false;
	}

	private static bool TryCreateOutput( TypeDescription type, PropertyDescription property, SignalOutputAttribute attribute, out SignalPort port )
	{
		port = default;
		if ( property.PropertyType != typeof( SignalOutput ) || !property.CanRead )
		{
			Log.Warning( $"Signal output {type.FullName}.{property.Name} must be a readable SignalOutput property." );
			return false;
		}

		port = new SignalPort( property.Name, attribute.Name ?? property.Name, attribute.Default ) { Property = property };
		return true;
	}

	private static bool TryCreateInput( TypeDescription type, MethodDescription method, SignalInputAttribute attribute, out SignalPort port )
	{
		port = default;
		if ( method.ReturnType != typeof( void ) || !TryBindMethod( method, out var binding ) )
		{
			Log.Warning( $"Signal input {type.FullName}.{method.Name} has an unsupported signature. Use no parameters, or one bool / float / SignalEvent parameter, and return void." );
			return false;
		}

		var id = attribute.Id ?? method.Name;
		port = new SignalPort( id, attribute.Name ?? id, attribute.Default ) { Binding = binding };
		return true;
	}

	private static bool TryCreateInput( TypeDescription type, PropertyDescription property, SignalInputAttribute attribute, out SignalPort port )
	{
		port = default;
		if ( !TryBindProperty( property, attribute, out var binding ) )
		{
			Log.Warning( $"Signal input {type.FullName}.{property.Name} has an unsupported type. Use bool, float, or register an ISignalAdapter." );
			return false;
		}

		var id = attribute.Id ?? property.Name;
		port = new SignalPort( id, attribute.Name ?? id, attribute.Default ) { Binding = binding };
		return true;
	}

	private static bool TryBindMethod( MethodDescription method, out SignalInputBinding binding )
	{
		binding = default;
		var parameters = method.Parameters;

		if ( parameters.Length == 0 )
		{
			binding = new SignalInputBinding( method, null, SignalInputKind.TriggerMethod );
			return true;
		}

		if ( parameters.Length > 1 ) return false;

		var parameterType = parameters[0].ParameterType;

		var kind = parameterType == typeof( SignalEvent ) ? SignalInputKind.EventMethod
			: parameterType == typeof( bool ) ? SignalInputKind.BoolMethod
			: parameterType == typeof( float ) ? SignalInputKind.FloatMethod
			: (SignalInputKind?)null;
		if ( kind is null ) return false;

		binding = new SignalInputBinding( method, null, kind.Value );

		return true;
	}

	private static bool TryBindProperty( PropertyDescription property, SignalInputAttribute attribute, out SignalInputBinding binding )
	{
		binding = default;

		if ( property.PropertyType == typeof( bool ) && property.CanWrite )
		{
			binding = new SignalInputBinding( null, property, SignalInputKind.BoolProperty );
			return true;
		}

		if ( property.PropertyType == typeof( float ) && property.CanWrite )
		{
			binding = new SignalInputBinding( null, property, SignalInputKind.FloatProperty );
			return true;
		}

		if ( FindAdapter( property.PropertyType ) is { } adapter )
		{
			try
			{
				return adapter.TryBind( new SignalInputContext( property, attribute ), out binding );
			}
			catch ( Exception exception )
			{
				Log.Warning( $"Signal adapter for {property.PropertyType.FullName} failed to bind {property.Name}: {exception.Message}" );
			}
		}

		return false;
	}

	private static ISignalAdapter FindAdapter( Type targetType )
	{
		ISignalAdapter result = null;

		foreach ( var description in Game.TypeLibrary.GetTypes<ISignalAdapter>().Where( type => !type.IsAbstract ).OrderBy( type => type.FullName, StringComparer.Ordinal ) )
		{
			var adapter = Game.TypeLibrary.Create<ISignalAdapter>( description.TargetType );
			if ( adapter?.TargetType is null )
			{
				Log.Warning( $"Signal adapter {description.FullName} could not be created. Adapters need a parameterless constructor and a TargetType." );
				continue;
			}

			if ( adapter.TargetType != targetType ) continue;
			if ( result is not null )
			{
				Log.Warning( $"Signal adapters for {targetType.Name} are ambiguous; refusing to choose one." );
				return null;
			}

			result = adapter;
		}

		return result;
	}
}
