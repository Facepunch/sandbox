namespace Sandbox;

/// <summary>
/// Paints the "Owned by" label under the crosshair in the HUD style
/// </summary>
public sealed class OwnerLabelSection : HudSection
{
	/// <summary>
	/// The text to show, null hides the label
	/// </summary>
	public string Text { get; set; }

	const float FontSize = 20;

	// same padding as the vitals boxes
	const float Padding = 15;
	const float SidePadding = Padding * 2.5f;

	// generous room for the glow around the box
	const float GlowMargin = 48;

	Rect _box;

	public override void GetCompositeRegions( List<Rect> regions )
	{
		if ( string.IsNullOrEmpty( Text ) || _box.Width <= 0 ) return;

		var margin = GlowMargin * ScaleToScreen;
		var box = _box.Grow( margin );
		regions.Add( new Rect( CanvasRect.Left + box.Left, CanvasRect.Top + box.Top, box.Width, box.Height ) );
	}

	public override void Paint( Painter painter )
	{
		if ( string.IsNullOrEmpty( Text ) ) return;

		var scale = ScaleToScreen;
		var bounds = painter.Bounds;

		using var _ = painter.Scope();
		PaintTint = Tint;
		painter.TextStyle = new TextStyle( HudStyle.Font, FontSize * scale, HudStyle.Brightened( Tinted( HudStyle.TitleColor ), TextGain ) ) { FontWeight = 700, Alignment = TextFlag.Center };

		var text = painter.MeasureText( Text );
		var pad = MathF.Round( Padding * scale );
		var sidePad = MathF.Round( SidePadding * scale );

		var size = new Vector2( MathF.Ceiling( text.x ) + sidePad * 2, MathF.Ceiling( text.y ) + pad * 2 );
		// level with the bottom of the vitals and ammo boxes
		var bottom = MathF.Floor( bounds.Height - Screen.Width * 0.02f );
		_box = new Rect( MathF.Floor( (bounds.Width - size.x) * 0.5f ), bottom - size.y, size.x, size.y );

		HudStyle.DrawPanel( painter, _box, HudStyle.PanelColor, scale );
		painter.Text( Text, _box );
	}
}
