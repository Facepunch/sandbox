using Sandbox.UI;
namespace Sandbox;

public partial class BaseSpawnMenu : Panel
{
	SpawnMenuOption activeOption;
	PanelSwitcher Switcher = default;
	protected Panel ActivePanel => activeOption?.Panel;

	protected Panel MenuFooter;
	bool _addedFooter;

	Panel TreeContainer;
	readonly TreeView<SpawnMenuOption> Tree = new();

	/// <summary>
	/// Top level options and headers.
	/// </summary>
	readonly List<SpawnMenuOption> roots = new();

	/// <summary>
	/// The header new options are added under, or null for the root.
	/// </summary>
	SpawnMenuOption currentHeader;

	/// <summary>
	/// Headers we've opened once already. New headers start open, after that the tree remembers.
	/// </summary>
	readonly HashSet<string> knownHeaders = new();

	public BaseSpawnMenu()
	{
		Tree.Roots = roots;
		Tree.Comparer = new OptionComparer();
		Tree.GetChildren = o => o.Children;
		Tree.GetParent = o => o.Parent;
		Tree.OnRow = BindRow;
		Tree.OnSelect = OnTreeSelect;
		Tree.OnContextMenu = o => o.OnRightClick?.Invoke();
	}

	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		roots.Clear();
		currentHeader = null;
		Rebuild();
		Tree.Refresh();
		StateHasChanged();
	}

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );

		if ( !_addedFooter && MenuFooter.IsValid() )
		{
			_addedFooter = true;
			OnMenuFooter( MenuFooter );
		}

		if ( TreeContainer.IsValid() && Tree.Parent != TreeContainer )
		{
			TreeContainer.AddChild( Tree );
		}

		Tree.RebindRows();
	}

	protected bool _firstViewed;

	public override void Tick()
	{
		base.Tick();

		if ( !IsVisible ) return;
		if ( _firstViewed ) return;
		if ( roots.Count == 0 ) return;
		if ( !Switcher.IsValid() ) return;

		_firstViewed = true;
		SwitchOption( AllOptions().FirstOrDefault( x => x.PanelCreator != null || x.Panel != null ) );
	}


	protected virtual void Rebuild()
	{

	}

	protected virtual void OnMenuFooter( Panel footer )
	{

	}

	/// <summary>
	/// Start a collapsible section. Options added after this go inside it.
	/// </summary>
	public void AddHeader( string name )
	{
		currentHeader = new SpawnMenuOption { Name = name };
		roots.Add( currentHeader );

		if ( knownHeaders.Add( name ) ) Tree.Open( currentHeader );
	}

	SpawnMenuOption Add( SpawnMenuOption o )
	{
		o.Parent = currentHeader;
		(currentHeader?.Children ?? roots).Add( o );
		return o;
	}

	public SpawnMenuOption AddOption( string name, Func<Panel> createPanelFunction )
	{
		return Add( new SpawnMenuOption
		{
			Name = name,
			PanelCreator = createPanelFunction
		} );
	}

	public SpawnMenuOption AddOption( string icon, string name, Func<Panel> createPanelFunction )
	{
		return Add( new SpawnMenuOption
		{
			Icon = icon,
			Name = name,
			PanelCreator = createPanelFunction
		} );
	}

	public SpawnMenuOption AddOption( string icon, string name, Func<Panel> createPanelFunction, Action onRightClick )
	{
		return Add( new SpawnMenuOption
		{
			Icon = icon,
			Name = name,
			PanelCreator = createPanelFunction,
			OnRightClick = onRightClick
		} );
	}

	public void AddAction( string icon, string name, Action action )
	{
		Add( new SpawnMenuOption
		{
			Icon = icon,
			Name = name,
			OnClick = action
		} );
	}

	IEnumerable<SpawnMenuOption> AllOptions()
	{
		foreach ( var o in roots )
		{
			yield return o;
			foreach ( var child in o.Children ) yield return child;
		}
	}

	void OnTreeSelect( SpawnMenuOption o )
	{
		// Clicking empty space drops the selection but still reports the cursor row
		if ( !Tree.IsSelected( o ) ) return;

		if ( o.Children.Count > 0 )
		{
			Tree.Toggle( o );
			return;
		}

		if ( !o.Enabled ) return;

		if ( o.OnClick != null )
		{
			o.OnClick.Invoke();
			return;
		}

		SwitchOption( o );
	}

	void BindRow( TreeRow row, SpawnMenuOption o )
	{
		row.SetClass( "active", activeOption is not null && Tree.Comparer.Equals( o, activeOption ) );
		row.SetClass( "disabled", !o.Enabled );

		row.IconName = o.Icon;
		row.Label.Tokenize = o.Tokenize;
		row.Text = o.Name;
	}

	protected void SwitchOption( SpawnMenuOption o )
	{
		if ( o == activeOption ) return;

		activeOption?.Panel?.SetClass( "hidden", true );

		activeOption = o;

		if ( activeOption.Panel == null && activeOption.PanelCreator != null )
		{
			activeOption.Panel = activeOption.PanelCreator.Invoke();
			Switcher.AddChild( activeOption.Panel );
		}

		activeOption?.Panel?.SetClass( "hidden", false );
		Tree.RebindRows();
		StateHasChanged();
	}

	public void SelectOption( string name )
	{
		var option = AllOptions().FirstOrDefault( o => o.Name == name && (o.PanelCreator != null || o.Panel != null) );
		if ( option != null ) SwitchOption( option );
	}

	public void DeselectOption()
	{
		activeOption?.Panel?.SetClass( "hidden", true );
		activeOption = null;
		Tree.RebindRows();
		StateHasChanged();
	}

	public class SpawnMenuOption
	{
		public string Name { get; set; }

		/// <summary>
		/// Whether the name is allowed to reference a localization phrase.
		/// Disable this for user-authored entries.
		/// </summary>
		public bool Tokenize { get; set; } = true;

		public string Icon { get; set; }
		public bool Enabled { get; set; } = true;

		public Func<Panel> PanelCreator { get; set; }
		public Panel Panel { get; set; }
		public Action OnClick { get; set; }
		public Action OnRightClick { get; set; }

		public SpawnMenuOption Parent { get; set; }
		public List<SpawnMenuOption> Children { get; } = new();
	}

	/// <summary>
	/// Options are recreated on every rebuild. Match them by name so open headers stay open.
	/// </summary>
	class OptionComparer : IEqualityComparer<SpawnMenuOption>
	{
		public bool Equals( SpawnMenuOption a, SpawnMenuOption b ) => a?.Name == b?.Name;
		public int GetHashCode( SpawnMenuOption o ) => o.Name?.GetHashCode() ?? 0;
	}
}

