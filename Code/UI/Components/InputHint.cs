using Sandbox.UI;

namespace Sandbox;

public class InputHint : Panel
{
	Texture _lastGlyph;
	string _lastOrigin;

	[Parameter] public string Action { get; set; }
	[Parameter] public InputGlyphSize GlyphSize { get; set; } = InputGlyphSize.Medium;

	public override void Tick()
	{
		if ( string.IsNullOrEmpty( Action ) ) return;

		var glyph = Input.GetGlyph( Action, GlyphSize, false );

		if ( glyph.IsValid() )
		{
			if ( glyph == _lastGlyph ) return;
			_lastGlyph = glyph;
			_lastOrigin = null;

			Style.SetBackgroundImage( glyph );
			Style.AspectRatio = (float)glyph.Width / glyph.Height;
		}
		else
		{
			var origin = Input.GetButtonOrigin( Action ) ?? Action;
			if ( origin == _lastOrigin ) return;
			_lastOrigin = origin;
			_lastGlyph = null;

			Style.SetBackgroundImage( string.Empty );
		}
	}
}
