/// <summary>
/// Lets scene components and systems add actions to an object's right-click menu.
/// Options are collected with a sort order (lower is higher in the menu), then the Inspector builds the menu.
/// The built-ins use 100 (Ignite/Extinguish), 200 (Delete) and 300 (Break); the default of 0 lands above them.
/// </summary>
public interface IContextMenuEvent : ISceneEvent<IContextMenuEvent>
{
	public sealed record Option( string Icon, string Text, Action Action, Action<Sandbox.UI.Menu> SubmenuBuilder, int Order, bool IsSeparator = false );

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

		public void AddSubmenu( string icon, string text, Action<Sandbox.UI.Menu> builder, int order = 0 )
		{
			_options.Add( new Option( icon, text, null, builder, order ) );
		}

		/// <summary>
		/// A line between options. Put one on each side of a group; separators at the top or bottom of the menu, or
		/// next to another one, are left out.
		/// </summary>
		public void AddSeparator( int order = 0 )
		{
			_options.Add( new Option( null, null, null, null, order, IsSeparator: true ) );
		}

		/// <summary>
		/// Whether there's anything to show besides separators.
		/// </summary>
		public bool HasOptions => _options.Any( o => !o.IsSeparator );

		/// <summary>
		/// Build the collected options into a menu.
		/// </summary>
		public void Populate( Sandbox.UI.Menu menu )
		{
			var addedAny = false;
			var separatorPending = false;

			foreach ( var option in Options )
			{
				if ( option.IsSeparator )
				{
					separatorPending = addedAny;
					continue;
				}

				if ( separatorPending )
				{
					menu.AddSeparator();
					separatorPending = false;
				}

				if ( option.SubmenuBuilder is not null )
					option.SubmenuBuilder( menu.AddMenu( option.Text, option.Icon ) );
				else
					menu.AddOption( option.Text, option.Icon, option.Action );

				addedAny = true;
			}
		}
	}

	void PopulateContextMenu( Event e ) { }
}
