namespace Sandbox;

public static class Hud
{
	public static float Scale => Screen.Height / 1080.0f;

	/// <summary>
	/// Space kept between the vitals and the HUD stacked above them (chat, notices, the voice list)
	/// </summary>
	const float StackGap = 10;

	public static float StackBottom
	{
		get
		{
			var vitals = Game.ActiveScene?.Get<HudLayer>()?.Canvas?.Section<VitalsSection>( false );

			// until the vitals have been painted, go by the size they usually are
			var scale = vitals?.Canvas.ScaleToScreen ?? Scale;
			var fromBottom = vitals is { BoxTop: > 0 } ? vitals.Canvas.PixelSize.y - vitals.BoxTop : Screen.Width * 0.02f + 92 * scale;

			return fromBottom + StackGap * scale;
		}
	}

	private static float _cachedAt = -1;
	private static HudElement _hidden;

	/// <summary>
	/// True if none of the given elements are hidden. HUD panels call this before drawing. The answer
	/// comes from every <see cref="IHudEvents"/> listener in the scene, asked once per frame.
	/// </summary>
	public static bool IsVisible( HudElement elements ) => (Hidden & elements) == 0;

	private static HudElement Hidden
	{
		get
		{
			if ( _cachedAt == RealTime.Now )
				return _hidden;

			_cachedAt = RealTime.Now;
			_hidden = HudElement.None;

			var scene = Game.ActiveScene;
			if ( !scene.IsValid() )
				return _hidden;

			var hidden = HudElement.None;
			scene.RunEvent<IHudEvents>( x => x.OnHudVisibility( ref hidden ) );
			_hidden = hidden;

			return _hidden;
		}
	}
}
