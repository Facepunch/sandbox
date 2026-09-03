namespace Sandbox;

public static class Hud
{
	public static float Scale => Screen.Height / 1080.0f;

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
