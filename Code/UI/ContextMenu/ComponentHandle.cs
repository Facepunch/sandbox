
using Sandbox.UI;
namespace Sandbox;

/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
public class ComponentHandle : Panel
{
	readonly Component _component;
	readonly Label _icon;
	Vector3 worldpos;

	public ComponentHandle( Panel parent, Component c ) : base( parent )
	{
		_component = c;
		worldpos = _component.WorldPosition;

		_icon = AddChild<Label>( "icon" );

		var typeInfo = TypeLibrary.GetType( c.GetType() );
		_icon.Text = typeInfo.Icon ?? "ℹ️";
	}

	public override void Tick()
	{
		base.Tick();

		if ( _component.IsValid() )
		{
			worldpos = _component.WorldPosition;
		}

		{
			var screenPos = Scene.Camera.PointToScreenPixels( worldpos, out bool behind );
			Style.Left = screenPos.x * ScaleFromScreen;
			Style.Top = screenPos.y * ScaleFromScreen;

			SetClass( "behind", behind );
		}

		if ( ShouldDelete() )
		{
			Delete();
		}
	}

	bool ShouldDelete()
	{
		if ( !_component.IsValid() ) return true;

		return false;
	}

}
