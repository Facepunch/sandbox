namespace Sandbox;

/// <summary>
/// One part of the HUD (vitals, weapon buckets etc) painted into the shared HudCanvas
/// </summary>
public abstract class HudSection
{
	public HudCanvas Canvas { get; internal set; }

	/// <summary>
	/// Paint order, lowest first
	/// </summary>
	public virtual int Order => 0;

	public virtual void Tick() { }

	/// <summary>
	/// Paint this section
	/// </summary>
	public abstract void Paint( Painter painter );

	/// <summary>
	/// Screen rects the composite shader runs over. Everything painted (plus its glow) must sit inside them.
	/// </summary>
	public abstract void GetCompositeRegions( List<Rect> regions );

	protected float ScaleToScreen => Canvas.ScaleToScreen;

	protected Rect CanvasRect => Canvas.Box.Rect;

	protected Color Tint => Canvas.Layer.Tint;
	protected float DigitGain => Canvas.DigitGain;
	protected float TextGain => Canvas.TextGain;
	protected float GlowRadius => Canvas.Layer.GlowRadius;

	/// <summary>
	/// Background color for HUD elements
	/// </summary>
	protected static readonly Color PanelColor = new Color( 0.07f, 0.5f );

	protected internal Color PaintTint { get; set; } = Color.White;

	protected Color Tinted( Color color ) => color * PaintTint;

	protected Color Glowing( Color color ) => Brightened( color, DigitGain );

	/// <summary>
	/// Scales the colour, leaving its alpha alone. A gain past 1 goes over 1 for HDR glow, below 1 darkens.
	/// </summary>
	protected static Color Brightened( Color color, float gain ) => (color * gain).WithAlpha( color.a );

	/// <summary>
	/// Corner radius of <see cref="DrawPanel"/>
	/// </summary>
	const float PanelRadius = 2;

	const float PanelSoftness = 0.5f;

	/// <summary>
	/// A flat panel with rounded corners
	/// </summary>
	protected static void DrawPanel( Painter painter, Rect rect, Color color, float scale )
	{
		var softness = PanelSoftness * scale;
		var radius = PanelRadius * scale;

		painter.RectShadow( rect, radius, color, softness * 2, softness );

		painter.Fill = color;
		painter.Rect( rect, radius );
	}
}
