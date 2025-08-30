using Sandbox.UI;

public partial class BaseSpawnMenu : Panel
{
	SpawnMenuOption activeOption;
	PanelSwitcher Switcher;

	void Spawn( string ident )
	{
		Log.Info( $"Spawning {ident}" );

		GameManager.Spawn( ident );
	}

	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		options.Clear();
		Rebuild();
	}

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );

		if ( firstTime && Switcher.IsValid() )
		{
			SwitchOption( options.First() );
		}
	}

	protected virtual void Rebuild()
	{

	}

	public void AddOption( string name, Func<Panel> createPanelFunction )
	{
		var o = new SpawnMenuOption
		{
			Name = name,
			PanelCreator = createPanelFunction
		};

		options.Add( o );
		StateHasChanged();
	}

	void SwitchOption( SpawnMenuOption o )
	{
		if ( o == activeOption ) return;

		activeOption?.Panel?.SetClass( "hidden", true );

		activeOption = o;

		if ( activeOption.Panel == null )
		{
			activeOption.Panel = activeOption.PanelCreator.Invoke();
			Switcher.AddChild( activeOption.Panel );
		}

		activeOption.Panel.SetClass( "hidden", false );
		StateHasChanged();
	}


	class SpawnMenuOption
	{
		public string Name { get; set; }
		public string Icon { get; set; }
		public Func<Panel> PanelCreator { get; set; }
		public Panel Panel { get; set; }
	}

	List<SpawnMenuOption> options = new();
}

