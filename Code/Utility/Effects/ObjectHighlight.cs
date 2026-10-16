/// <summary>
/// How an object highlight looks: its outline colour, the colour where something closer hides it, and its width.
/// </summary>
public readonly record struct HighlightStyle( Color Color, Color ObscuredColor, float Width = 0.2f )
{
	/// <summary>
	/// Something we're acting on - held by the physgun, or selected in the inspector.
	/// </summary>
	public static readonly HighlightStyle Selected = new( new Color( 4.7f, 10.1f, 30.6f, 1 ), new Color( 2.2f, 2.3f, 2.9f, 0.1f ) );

	/// <summary>
	/// Something under the inspector's cursor
	/// </summary>
	public static readonly HighlightStyle Hovered = new( new Color( 2.6f, 2.0f, 0.2f, 1 ), new Color( 2.6f, 2.0f, 0.2f, 0.1f ) );
}

public static class ObjectHighlight
{
	static Material _material;

	/// <summary>
	/// The opacity-aware highlight material every sandbox outline draws with.
	/// </summary>
	public static Material Material => _material ??= Material.FromShader( "shaders/postprocess/sandbox_highlight.shader" );

	/// <summary>
	/// Add a hidden outline to <paramref name="host"/>, to point at things later with <see cref="Show(HighlightOutline, GameObject, HighlightStyle)"/>.
	/// </summary>
	public static HighlightOutline Create( GameObject host )
	{
		var outline = host.AddComponent<HighlightOutline>( false );
		outline.OverrideTargets = true;
		outline.Targets = new();
		outline.Material = Material;
		return outline;
	}

	/// <summary>
	/// Outline <paramref name="target"/>, or hide the outline if it's not valid.
	/// </summary>
	public static void Show( this HighlightOutline outline, GameObject target, HighlightStyle style )
	{
		Show( outline, [target], style );
	}

	/// <summary>
	/// Outline <paramref name="targets"/>, or hide the outline if there are none.
	/// </summary>
	public static void Show( this HighlightOutline outline, IEnumerable<GameObject> targets, HighlightStyle style )
	{
		if ( !outline.IsValid() ) return;

		outline.Material = Material;
		outline.OverrideTargets = true;
		outline.Color = style.Color;
		outline.ObscuredColor = style.ObscuredColor;
		outline.Width = style.Width;

		outline.Targets ??= new();
		outline.Targets.Clear();

		foreach ( var target in targets )
		{
			AddRenderers( target, outline.Targets );
		}

		outline.Enabled = outline.Targets.Count > 0;
	}

	/// <summary>
	/// Stop outlining anything.
	/// </summary>
	public static void Hide( this HighlightOutline outline )
	{
		if ( !outline.IsValid() ) return;

		outline.Targets?.Clear();
		outline.Enabled = false;
	}

	/// <summary>
	/// Briefly flash an outline around <paramref name="target"/>, fading in to <paramref name="brightness"/> times
	/// the style's colour and back out over <paramref name="duration"/> seconds.
	/// </summary>
	public static void Flash( GameObject target, HighlightStyle style, float duration = 0.25f, float brightness = 3.0f )
	{
		if ( !target.IsValid() ) return;

		var go = new GameObject( true, "Outline Flash" );
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;

		var outline = go.AddComponent<HighlightOutline>();
		outline.Show( target, style );

		var flash = go.AddComponent<OutlineFlash>();
		flash.Style = style;
		flash.Duration = duration;
		flash.Brightness = brightness;
		flash.Play( outline );
	}

	static void AddRenderers( GameObject o, List<Renderer> renderers )
	{
		if ( !o.IsValid() ) return;

		renderers.AddRange( o.GetComponents<Renderer>() );

		foreach ( var c in o.Children )
		{
			if ( c.NetworkMode == NetworkMode.Object ) continue;

			AddRenderers( c, renderers );
		}
	}
}
