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

		var method = _memberProperty.GetCustomizable();
		method.SetDisplayName( "Method" );

		var cs = new ControlSheet();
		cs.AddRow( _invokeType );

		if ( _invokeType.GetValue<Doo.InvokeType>() == Doo.InvokeType.Member )
		{
			cs.AddRow( _targetComponent );
			var methodSelect = cs.AddControl<ComponentMethodSelector>( method );
			methodSelect.TargetComponent = _targetComponent.GetValue<Component>();
		}
		else
		{
			var methodSelect = cs.AddControl<MethodSelector>( method );
		}

		Layout.Add( cs );

		var methodDesc = Doo.Helpers.FindMethod( _memberProperty.As.String );
		if ( methodDesc == null ) return;

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
				csp.AddAttribute( new Doo.TypeHintAttribute( param.ParameterType ) );

				cs.AddRow( csp );
				i++;
				//	var paramProp = Target.GetProperty( nameof( Doo.Block.Parameters ) ).ElementAt( param.Index );
				//	Layout.Add( new ParameterControlWidget( paramProp, param ) );
			}
		}
	}
}

public class ComponentMethodSelector : MethodSelector
{
	public Component TargetComponent { get; set; }

	public ComponentMethodSelector( SerializedProperty p ) : base( p )
	{

	}

	protected override void BuildMethods( AdvancedDropdownItem root )
	{
		if ( TargetComponent is null )
			return;

		var t = TypeLibrary.GetType( TargetComponent.GetType().FullName );

		var members = t.Members.Where( ShouldShow ).ToArray();

		foreach ( var group in members.GroupBy( x => x.DeclaringType?.Name ).OrderBy( x => x.Key ) )
		{
			var category = root.Add( group.Key );

			foreach ( var m in group.OrderBy( x => x.Name ) )
			{
				if ( m is PropertyDescription pd )
				{
					/*
					category.Add( new AdvancedDropdownItem
					{
						Title = "Get " + m.Name,
						Description = m.Name,
						Value = pd,
						Icon = "keyboard_double_arrow_left"
					} );

					category.Add( new AdvancedDropdownItem
					{
						Title = "Set " + m.Name,
						Description = m.Name,
						Value = pd,
						Icon = "keyboard_double_arrow_right"
					} );
					*/
				}
				else
				{
					category.Add( new AdvancedDropdownItem
					{
						Title = m.Name,
						Description = m.Name,
						Value = m
					} );
				}
			}
		}
	}

	private bool ShouldShow( MemberDescription description )
	{
		if ( description is not MethodDescription && description is not PropertyDescription ) return false;
		if ( !description.IsPublic ) return false;
		if ( description.DeclaringType == null ) return false;

		if ( description is PropertyDescription pd )
		{
			if ( pd.PropertyType == typeof( Action ) ) return false;

		}

		return true;
	}
}

public class MethodSelector : ControlWidget
{
	public MethodSelector( SerializedProperty p ) : base( p )
	{
		Layout = Layout.Row();
		Layout.AddStretchCell();

		{
			var icon = Layout.Add( new IconButton( "folder_open" ) );
			icon.Background = Color.Transparent;
			icon.OnClick = OpenSelector;
		}
	}

	public void OpenSelector()
	{
		var popup = new AdvancedDropdownPopup( this );
		popup.Dropdown.RootTitle = "Method";
		popup.Dropdown.SearchPlaceholderText = "Find Methods";
		popup.Dropdown.OnBuildItems = BuildMethods;

		popup.Dropdown.OnSelect = ( value ) =>
		{
			if ( value is MethodDescription md )
			{
				SerializedProperty.SetValue( $"{md.TypeDescription.FullName}.{md.Name}" );
			}
		};
		popup.Dropdown.Rebuild();
		popup.OpenAtCursor();
	}


	protected virtual void BuildMethods( AdvancedDropdownItem root )
	{
		var methods = TypeLibrary.GetMethodsWithAttribute<Doo.StaticMethodAttribute>( true );

		foreach ( var group in methods.GroupBy( x => x.Attribute.CategoryName ).OrderBy( x => x.Key ) )
		{
			var category = root.Add( group.Key );

			foreach ( var m in group.OrderBy( x => x.Attribute.Path ) )
			{
				category.Add( new AdvancedDropdownItem
				{
					Title = m.Attribute.Path,
					Description = m.Method.TypeDescription?.FullName,
					Value = m.Method
				} );
			}
		}
	}

	protected override void PaintControl()
	{
		var methodpath = SerializedProperty.GetValue<string>()?.ToString();
		if ( string.IsNullOrWhiteSpace( methodpath ) )
		{
			Paint.SetPen( Theme.Text.WithAlpha( 0.5f ) );
			Paint.DrawText( LocalRect.Shrink( 8, 0 ), "No Method Selected", TextFlag.LeftCenter );
			return;
		}

		var methodDesc = Doo.Helpers.FindMethod( methodpath );
		if ( methodDesc == null )
		{
			Paint.SetPen( Theme.Text.WithAlpha( 0.5f ) );
			Paint.DrawText( LocalRect.Shrink( 8, 0 ), $"Missing: {methodpath}", TextFlag.LeftCenter );
			return;
		}

		if ( methodDesc.GetCustomAttribute<Doo.StaticMethodAttribute>() is Doo.StaticMethodAttribute attr )
		{
			Paint.SetPen( Theme.Text );
			Paint.DrawText( LocalRect.Shrink( 8, 0 ), attr.Path, TextFlag.LeftCenter );
			return;
		}

		Paint.SetPen( Theme.Text );
		Paint.DrawText( LocalRect.Shrink( 8, 0 ), methodDesc.ToString(), TextFlag.LeftCenter );
	}
}

