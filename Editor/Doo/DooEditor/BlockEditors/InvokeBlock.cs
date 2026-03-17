using System;

namespace Sandbox;

/// <summary>
/// Main window for editing Doo scripts.
/// </summary>
[Inspector( typeof( Doo.InvokeBlock ) )]
public class InvokeBlock : InspectorWidget
{
	SerializedObject Target { get; }

	readonly SerializedProperty _invokeType;
	readonly SerializedProperty _targetComponent;
	readonly SerializedProperty _memberProperty;
	readonly SerializedProperty _argumentsProperty;

	public InvokeBlock( SerializedObject obj ) : base( obj )
	{
		Target = obj;
		Layout = Layout.Column();
		Layout.Spacing = 4;

		_invokeType = Target.GetProperty( nameof( Doo.InvokeBlock.InvokeType ) );
		_invokeType.OnChanged += ( p ) => BuildUI();

		_targetComponent = Target.GetProperty( nameof( Doo.InvokeBlock.TargetComponent ) );
		_targetComponent.OnChanged += ( p ) => BuildUI();

		_memberProperty = Target.GetProperty( nameof( Doo.InvokeBlock.Member ) );
		_memberProperty.OnChanged += ( p ) => BuildUI();

		_argumentsProperty = Target.GetProperty( nameof( Doo.InvokeBlock.Arguments ) );

		BuildUI();
	}

	void BuildUI()
	{
		Layout.Clear( true );

		var invokeType = _invokeType.GetValue<Doo.InvokeType>();
		var hasComponent = _targetComponent.GetValue<Component>() != null;

		var method = _memberProperty.GetCustomizable();
		method.SetDisplayName( "Method" );

		{
			var type = _invokeType.GetCustomizable();
			type.AddAttribute( new WideModeAttribute() { HasLabel = false } );

			var header = new ControlSheet();
			Layout.Add( header );
			header.AddRow( type );
		}

		Layout.AddSpacingCell( 16 );

		var cs = new ControlSheet();
		Layout.Add( cs );

		if ( invokeType == Doo.InvokeType.Member )
		{
			cs.AddRow( _targetComponent );

			if ( hasComponent )
			{
				var methodSelect = cs.AddControl<ComponentMethodSelector>( method );
				methodSelect.TargetComponent = _targetComponent.GetValue<Component>();
			}
		}
		else
		{
			var methodSelect = cs.AddControl<MethodSelector>( method );
		}

		// member invoke and no component
		if ( invokeType == Doo.InvokeType.Member && !hasComponent ) return;

		var methodDesc = Doo.Helpers.FindMethod( _memberProperty.As.String );
		if ( methodDesc == null ) return;

		// member invoke and not found on component!
		if ( invokeType == Doo.InvokeType.Member )
		{
			var target = _targetComponent.GetValue<Component>();
			if ( target == null ) return;

			if ( !methodDesc.DeclaringType.TargetType.IsAssignableFrom( target.GetType() ) )
				return;

			return;
		}

		List<SerializedProperty> arguments = [];

		if ( _argumentsProperty.TryGetAsObject( out var obj ) && obj is SerializedCollection sc )
		{
			if ( sc.Count() != methodDesc.Parameters.Length )
			{
				while ( sc.Count() > 0 )
					sc.RemoveAt( 0 );

				for ( int a = 0; a < methodDesc.Parameters.Length; a++ )
					sc.Add( null );
			}

			if ( methodDesc.Parameters.Length == 0 )
			{
				return;
			}

			var headerLo = Layout.Column();
			headerLo.Add( new Label.Header( "Parameters" ) );
			cs.AddLayout( headerLo );

			int i = 0;
			foreach ( var param in methodDesc.Parameters )
			{
				var prop = obj.ToArray()[i];

				var csp = prop.GetCustomizable();
				csp.SetDisplayName( $"{param.Name.ToTitleCase()}" );
				csp.AddAttribute( new TypeHintAttribute( param.ParameterType ) );

				cs.AddRow( csp );
				i++;
				//	var paramProp = Target.GetProperty( nameof( Doo.Block.Parameters ) ).ElementAt( param.Index );
				//	Layout.Add( new ParameterControlWidget( paramProp, param ) );
			}
		}
	}
}

