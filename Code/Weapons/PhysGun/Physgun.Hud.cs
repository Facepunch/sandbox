using Sandbox.Rendering;

public partial class Physgun : ScreenWeapon
{
	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( _state.IsValid() )
			return;

		if ( _stateHovered.IsValid() )
		{
			painter.DrawCircle( crosshair, 3, Color.Cyan );
		}
		else
		{
			painter.DrawCircle( crosshair, 5, Color.Cyan.WithAlpha( 0.2f ) );
		}


	}

}
