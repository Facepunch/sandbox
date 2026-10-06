using Sandbox.Utility;

/// <summary>
/// A short-lived outline that fades in, peaks at boosted brightness and fades back out.
/// </summary>
public sealed class OutlineFlash : Component
{
	public Color Color { get; set; }
	public Color ObscuredColor { get; set; }
	public float Width { get; set; } = 0.2f;
	public float Duration { get; set; } = 0.1f;

	/// <summary>
	/// Color multiplier at the peak of the flash effect
	/// </summary>
	public float Brightness { get; set; } = 2.0f;

	HighlightOutline _outline;
	TimeSince _timeSinceStart;

	/// <summary>
	/// Flash an outline around <paramref name="target"/>
	/// </summary>
	public static OutlineFlash Play( GameObject target, Color color, Color obscuredColor, float width = 0.2f, float duration = 0.1f, float brightness = 2.0f )
	{
		if ( !target.IsValid() ) return null;

		var go = new GameObject( true, "Outline Flash" );
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;

		var flash = go.AddComponent<OutlineFlash>();
		flash.Color = color;
		flash.ObscuredColor = obscuredColor;
		flash.Width = width;
		flash.Duration = duration;
		flash.Brightness = brightness;

		flash._timeSinceStart = 0;

		flash._outline = go.AddComponent<HighlightOutline>();
		flash._outline.OverrideTargets = true;
		flash._outline.Targets = new();
		AddRenderers( target, flash._outline.Targets );

		flash.Apply( 0 );
		return flash;
	}

	protected override void OnPreRender()
	{
		var t = Duration > 0 ? _timeSinceStart / Duration : 1;

		if ( t >= 1 )
		{
			GameObject.Destroy();
			return;
		}

		Apply( Easing.EaseInOut( 1 - MathF.Abs( t * 2 - 1 ) ) );
	}

	void Apply( float weight )
	{
		if ( !_outline.IsValid() ) return;

		var boost = MathX.Lerp( 1, Brightness, weight );

		_outline.Width = Width;
		_outline.Color = (Color * boost).WithAlpha( Color.a * weight );
		_outline.ObscuredColor = (ObscuredColor * boost).WithAlpha( ObscuredColor.a * weight );
	}

	/// <summary>
	/// Collect the renderers and its non-networked children
	/// </summary>
	public static void AddRenderers( GameObject o, List<Renderer> renderers )
	{
		renderers.AddRange( o.GetComponents<Renderer>() );

		foreach ( var c in o.Children )
		{
			if ( c.NetworkMode == NetworkMode.Object ) continue;

			AddRenderers( c, renderers );
		}
	}
}
