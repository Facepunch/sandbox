/// <summary>
/// Lets scene components and systems add menus to the strip of settings along the top of the inspect menu,
/// like Garry's Mod's context menu bar. Menus are found by title, so several listeners can add to the same one.
/// The built-ins use the order 100 (Client), 200 (NPCs) and 300 (Server); the default of 0 lands before them.
/// </summary>
public interface IMenuBarEvent : ISceneEvent<IMenuBarEvent>
{
	public sealed class Event
	{
		private readonly List<(Sandbox.UI.Menu Menu, int Order)> _menus = new();

		/// <summary>
		/// Menus collected so far, sorted by order. Stable, so menus sharing an order keep insertion order.
		/// </summary>
		public IEnumerable<Sandbox.UI.Menu> Menus => _menus.OrderBy( x => x.Order ).Select( x => x.Menu );

		/// <summary>
		/// Get the menu with this title, or add one. The order only counts when the menu is added.
		/// </summary>
		public Sandbox.UI.Menu GetOrAddMenu( string title, int order = 0 )
		{
			var existing = _menus.FirstOrDefault( x => x.Menu.Text == title ).Menu;
			if ( existing is not null ) return existing;

			var menu = new Sandbox.UI.Menu( title, null );
			_menus.Add( (menu, order) );
			return menu;
		}

		/// <summary>
		/// Add a checkable row that reads and writes a setting. Its check mark is refreshed every time the menu opens.
		/// </summary>
		public static Sandbox.UI.Menu AddToggle( Sandbox.UI.Menu menu, string text, Func<bool> get, Action<bool> set )
		{
			var option = menu.AddOption( text, value =>
			{
				// only write real changes, a refresh of the check mark isn't the player asking for one
				if ( value == get() ) return;
				set( value );
			} );

			option.Checked = get();
			menu.AboutToShow += _ => option.Checked = get();

			return option;
		}

		/// <summary>
		/// Add a checkable row for a server setting. Only the host can change it, everyone else sees it greyed out
		/// with the current value, the way Garry's Mod shows host-only settings.
		/// </summary>
		public static Sandbox.UI.Menu AddHostToggle( Sandbox.UI.Menu menu, string text, Func<bool> get, Action<bool> set )
		{
			var option = AddToggle( menu, text, get, set );

			UpdateHostOnly();
			menu.AboutToShow += _ => UpdateHostOnly();

			void UpdateHostOnly()
			{
				option.Enabled = Networking.IsHost;
				option.Tooltip = Networking.IsHost ? null : Game.Language.GetPhrase( "spawnmenu.common.host_only" );
			}

			return option;
		}
	}

	void PopulateMenuBar( Event e ) { }
}
