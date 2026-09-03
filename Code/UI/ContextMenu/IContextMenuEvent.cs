/// <summary>
/// Lets scene components and systems add actions to an object's right-click menu.
/// Options are collected with a sort order (lower is higher in the menu), then the Inspector builds the menu.
/// The built-ins use 100 (Ignite/Extinguish), 200 (Delete) and 300 (Break); the default of 0 lands above them.
/// </summary>
public interface IContextMenuEvent : ISceneEvent<IContextMenuEvent>
{
	public sealed record Option( string Icon, string Text, Action Action, Action<MenuPanel> SubmenuBuilder, int Order );

	public sealed class Event( GameObject target )
	{
		/// <summary>
		/// The network root of the object that was right-clicked.
		/// </summary>
		public GameObject Target { get; } = target;

		private readonly List<Option> _options = new();

		/// <summary>
		/// Options collected so far, sorted by order. Stable, so options sharing an order keep insertion order.
		/// </summary>
		public IEnumerable<Option> Options => _options.OrderBy( o => o.Order );

		public void AddOption( string icon, string text, Action action, int order = 0 )
		{
			_options.Add( new Option( icon, text, action, null, order ) );
		}

		public void AddSubmenu( string icon, string text, Action<MenuPanel> builder, int order = 0 )
		{
			_options.Add( new Option( icon, text, null, builder, order ) );
		}

		/// <summary>
		/// Build the collected options into a menu.
		/// </summary>
		public void Populate( MenuPanel menu )
		{
			foreach ( var option in Options )
			{
				if ( option.SubmenuBuilder is not null )
					menu.AddSubmenu( option.Icon, option.Text, option.SubmenuBuilder );
				else
					menu.AddOption( option.Icon, option.Text, option.Action );
			}
		}
	}

	void PopulateContextMenu( Event e ) { }
}
