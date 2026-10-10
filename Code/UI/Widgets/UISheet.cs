using Sandbox.Internal;
using Sandbox.UI;
using Sandbox.UI.Construct;

namespace Sandbox;

public class UISheet : Panel, IControlSheet
{
	/// <summary>
	/// The object whose properties we show, or a list of already serialized properties.
	/// </summary>
	[Parameter] public object Target { get; set; }

	/// <summary>
	/// Decides which properties get a row.
	/// </summary>
	[Parameter] public Func<SerializedProperty, bool> PropertyFilter { get; set; }

	Panel _body;
	int _hash;

	public UISheet()
	{
		AddClass( "sheet" );
	}

	/// <summary>
	/// Throws the rows away and makes them again from the target.
	/// </summary>
	public void Rebuild()
	{
		IControlSheet sheet = this;

		_body?.Delete( true );
		_body = AddChild<Panel>( "body" );

		if ( Target is null ) return;

		if ( Target is List<SerializedProperty> properties )
		{
			IControlSheet.FilterSortAndAdd( sheet, properties );
			return;
		}

		var serialized = Game.TypeLibrary.GetSerializedObject( Target );
		IControlSheet.FilterSortAndAdd( sheet, serialized.ToList() );
	}

	public override void Tick()
	{
		base.Tick();

		var hash = HashCode.Combine( Target );
		if ( hash == _hash && _body.IsValid() ) return;

		_hash = hash;
		Rebuild();
	}

	void IControlSheet.AddFeature( IControlSheet.Feature feature )
	{
		((IControlSheet)this).AddPropertiesWithGrouping( feature.Properties );
	}

	void IControlSheet.AddGroup( IControlSheet.Group group )
	{
		var panel = _body.AddChild<UISheetGroup>();
		panel.Initialize( group );
	}

	bool IControlSheet.TestFilter( SerializedProperty prop ) => PropertyFilter?.Invoke( prop ) ?? true;
}

/// <summary>
/// A titled run of rows. A toggle group has a checkbox in its title that shows and hides the rows.
/// </summary>
public class UISheetGroup : Panel
{
	Panel _header;
	Panel _rows;
	SerializedProperty _toggle;
	InspectorVisibilityAttribute[] _visibility;

	public UISheetGroup()
	{
		AddClass( "sheet-group" );
	}

	internal void Initialize( IControlSheet.Group group )
	{
		var title = group.Name;

		// the property named like the group switches it on and off
		var toggle = group.Properties.FirstOrDefault( x => x.HasAttribute<ToggleGroupAttribute>() && x.Name == group.Name );
		if ( toggle is not null )
		{
			toggle.TryGetAttribute<ToggleGroupAttribute>( out var attribute );
			group.Properties.Remove( toggle );

			title = attribute?.Label ?? title;
			_toggle = toggle;
			_visibility = toggle.GetAttributes<InspectorVisibilityAttribute>()?.ToArray();
		}

		if ( !string.IsNullOrWhiteSpace( title ) || _toggle is not null )
		{
			_header = Add.Panel( "header" );

			if ( _toggle is not null )
				_header.AddChild( new UICheckbox { Property = _toggle } );

			var label = _header.Add.Label( "", "title" );
			label.Text = title ?? "";
		}

		_rows = Add.Panel( "rows" );

		foreach ( var property in group.Properties )
		{
			var row = _rows.AddChild<UISheetRow>();
			row.Initialize( property );
		}
	}

	public override void Tick()
	{
		base.Tick();

		if ( _toggle is not null )
			_rows?.SetClass( "hidden", !_toggle.As.Bool );

		if ( _visibility?.Length > 0 )
			SetClass( "hidden", _visibility.All( x => x.TestCondition( _toggle?.Parent ) ) );
	}
}

/// <summary>
/// One property, its name on the left and its control on the right, wide controls like the resource grid go under the name instead.
/// </summary>
public class UISheetRow : Panel
{
	SerializedProperty _property;
	InspectorVisibilityAttribute[] _visibility;

	public UISheetRow()
	{
		AddClass( "sheet-row" );
	}

	internal void Initialize( SerializedProperty property )
	{
		_property = property;
		_visibility = property.GetAttributes<InspectorVisibilityAttribute>()?.ToArray();

		var control = CreateControl( property, out var wide );
		SetClass( "wide", wide );
		SetClass( "toggle", control is UICheckbox );

		// only the name shows the description, not the control next to it
		var label = AddChild( new UIHintLabel { Hint = property.Description } );
		label.AddClass( "label" );
		label.Text = property.DisplayName;

		var holder = Add.Panel( "control" );
		if ( control is not null )
			holder.AddChild( control );
	}

	public override void Tick()
	{
		base.Tick();

		if ( _visibility is null || _visibility.Length == 0 ) return;
		if ( _property?.Parent is null ) return;

		SetClass( "hidden", _visibility.All( x => x.TestCondition( _property.Parent ) ) );
	}

	/// <summary>
	/// Pick the control for a property. Anything we don't have a Sandbox one for gets the engine's.
	/// </summary>
	static BaseControl CreateControl( SerializedProperty property, out bool wide )
	{
		wide = false;

		if ( property.IsMethod )
			return BaseControl.CreateFor( property );

		var type = property.PropertyType;
		if ( type is null || Nullable.GetUnderlyingType( type ) is not null )
			return BaseControl.CreateFor( property );

		if ( type == typeof( bool ) )
			return new UICheckbox { Property = property };

		if ( type.IsEnum )
			return new UIEnumControl { Property = property };

		if ( type == typeof( Color ) )
			return new UIColor { Property = property };

		if ( type == typeof( string ) && property.HasAttribute<ResourceSelectAttribute>() )
		{
			wide = true;
			return new UIResourceGrid { Property = property };
		}

		var isNumber = type == typeof( float ) || type == typeof( double ) || type == typeof( int );
		if ( isNumber && property.HasAttribute<RangeAttribute>() )
			return new UISlider { Property = property };

		return BaseControl.CreateFor( property );
	}
}
