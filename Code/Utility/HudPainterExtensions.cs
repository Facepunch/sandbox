using Sandbox.Rendering;

public static class HudPainterExtensions
{
	/// <summary>
	/// Draws a hud element line to the screen, with text, and optionally an icon.
	/// </summary>
	public static void DrawHudElement( this HudPainter hud, string text, Vector2 position, Texture icon = null, float iconSize = 32f, TextFlag flags = TextFlag.LeftCenter )
	{
		var textScope = new TextRendering.Scope( text, Color.White, 32 * Hud.Scale );
		textScope.TextColor = "white";
		textScope.FontName = "Poppins";
		textScope.FontWeight = 600;
		textScope.Shadow = new TextRendering.Shadow { Enabled = true, Color = "#f506", Offset = 0, Size = 2 };

		hud.SetBlendMode( BlendMode.Lighten );

		if ( icon != null )
		{
			if ( flags.HasFlag( TextFlag.Right ) )
				position.x -= iconSize * Hud.Scale;

			hud.DrawTexture( icon, new Rect( position, iconSize * Hud.Scale ), textScope.TextColor );
		}

		const float padding = 16f;

		if ( flags.HasFlag( TextFlag.Left ) )
			position.x += (iconSize + padding) * Hud.Scale;

		var rect = new Rect( position, new Vector2( 256 * Hud.Scale, iconSize * Hud.Scale ) );
		if ( flags.HasFlag( TextFlag.Right ) )
			rect.Right = rect.Left - padding * Hud.Scale;

		hud.DrawText( textScope, rect, flags );
	}
}
