public partial class Physgun : ScreenWeapon
{
	/// <summary>
	/// Draws a targeting dot when the physgun is not holding an object.
	/// </summary>
	public override void DrawHud( Painter painter, Vector2 crosshair )
	{
		if ( _state.IsValid() )
			return;

		using var scope = painter.Scope();
		painter.BlendMode = BlendMode.Normal;
		painter.Stroke = Stroke.None;

		if ( _stateHovered.IsValid() )
		{
			painter.Fill = Color.Cyan;
			painter.Circle( crosshair, 1.5f );
		}
		else
		{
			painter.Fill = Color.Cyan.WithAlpha( 0.2f );
			painter.Circle( crosshair, 2.5f );
		}
	}
}
