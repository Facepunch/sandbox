using Sandbox.UI;

namespace Sandbox;

public static class Hud
{
	public static float Scale => Screen.Height / 1080.0f;

	/// <summary>
	/// Gap between the screen edges and the HUD in screen pixels, from the player's HUD margin
	/// </summary>
	public static float Margin => Screen.Width * GamePreferences.HudMargin / 100f;

	/// <summary>
	/// <see cref="Margin"/> for panel styles, which scale it to the panel themselves
	/// </summary>
	public static Length? MarginLength => Length.ViewWidth( GamePreferences.HudMargin );

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
			var scale = (vitals?.Canvas.ScaleToScreen ?? Scale) * GamePreferences.HudScale;
			var fromBottom = vitals is { BoxTop: > 0 } ? vitals.Canvas.PixelSize.y - vitals.BoxTop : Margin + 92 * scale;

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

			// the player's own HUD preferences, then whatever the scene hides on top
			_hidden = GamePreferences.HiddenHud;

			var scene = Game.ActiveScene;
			if ( !scene.IsValid() )
				return _hidden;

			var hidden = _hidden;
			scene.RunEvent<IHudEvents>( x => x.OnHudVisibility( ref hidden ) );
			_hidden = hidden;

			return _hidden;
		}
	}
}
