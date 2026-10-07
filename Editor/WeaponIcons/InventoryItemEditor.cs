using System;

namespace Sandbox.WeaponIcons;

/// <summary>
/// Inventory item inspector for Sandbox mode but with support for icon generator
/// </summary>
[CustomEditor( typeof( BaseInventoryItem ) )]
public class InventoryItemEditor : ComponentEditorWidget
{
	Editor.Button _createButton;
	RealTimeSince _lastSearch;

	public InventoryItemEditor( SerializedObject obj ) : base( obj )
	{
		Layout = Layout.Column();
		BuildSheet();
	}

	void BuildSheet()
	{
		Layout.Clear( true );
		_createButton = null;

		var showAdvanced = SerializedObject.Targets.OfType<Component>().Any( x => x.IsValid() && x.Flags.Contains( ComponentFlags.ShowAdvancedProperties ) );

		var sheet = new Editor.ControlSheet();
		sheet.IncludePropertyNames = true;
		sheet.AddObject( SerializedObject, p => FilterProperties( p, showAdvanced ) );
		Layout.Add( sheet );
	}

	static bool FilterProperties( SerializedProperty p, bool showAdvanced )
	{
		if ( p.PropertyType is null ) return false;
		if ( p.PropertyType.IsAssignableTo( typeof( Delegate ) ) && p.Name.StartsWith( "OnComponent" ) ) return false;
		if ( p.HasAttribute<AdvancedAttribute>() && !showAdvanced ) return false;
		if ( p.IsMethod ) return true;

		return p.HasAttribute<PropertyAttribute>();
	}

	[EditorEvent.Frame]
	void AttachCreateButton()
	{
		if ( _createButton.IsValid() ) return;
		if ( _lastSearch < 0.25f ) return;
		_lastSearch = 0;

		var wrapper = GetDescendants<ResourceWrapperControlWidget>()
			.FirstOrDefault( x => x.SerializedProperty?.Name == nameof( BaseInventoryItem.DisplayIcon ) );

		if ( wrapper is null ) return;

		_createButton = new Editor.Button( "Setup...", "add_photo_alternate" );
		_createButton.ToolTip = "Set up an inventory icon for this item";
		_createButton.Enabled = wrapper.SerializedProperty.IsEditable;
		_createButton.Clicked = OpenCreator;

		wrapper.Layout.Add( _createButton );
	}

	void OpenCreator()
	{
		var item = SerializedObject.Targets.OfType<BaseInventoryItem>().FirstOrDefault();
		if ( !item.IsValid() ) return;

		WeaponIconWindow.Open( item, SerializedObject.GetProperty( nameof( BaseInventoryItem.DisplayIcon ) ), () =>
		{
			if ( IsValid ) BuildSheet();
		} );
	}
}
