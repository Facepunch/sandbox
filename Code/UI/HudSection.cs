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

	protected float FontScale => Canvas.FontScale;

	protected Color Tint => Canvas.Tint;
	protected float DigitExponent => Canvas.DigitExponent;
	protected float TextExponent => Canvas.TextExponent;
	protected float GlowRadius => Canvas.GlowRadius;

	protected internal Color PaintTint { get; set; } = Color.White;

	protected Color Tinted( Color color ) => new( color.r * PaintTint.r, color.g * PaintTint.g, color.b * PaintTint.b, color.a * PaintTint.a );

	protected Color Glowing( Color color ) => Brightened( color, DigitExponent );

	protected static Color Brightened( Color color, float exponent )
	{
		var intensity = MathF.Pow( 2, exponent );
		return new Color( color.r * intensity, color.g * intensity, color.b * intensity, color.a );
	}
}
