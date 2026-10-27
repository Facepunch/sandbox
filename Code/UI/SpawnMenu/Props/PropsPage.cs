
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "#spawnmenu.tab.spawnlists" ), Order( 0 ), Icon( "📦" )]
public class PropsPage : SpawnlistsPage
{
	protected override void AddProceduralSpawnlists()
	{
		var builder = AddOption( "carpenter", "Building Props", () => new BuilderPage() );
		builder.Key = "builder";
		foreach ( var category in BuilderCatalog.Categories )
		{
			AddOption( builder, new SpawnMenuOption
			{
				Name = category,
				Key = $"builder/{category}",
				Icon = "category",
				PanelCreator = () => new BuilderPage { Category = category }
			} );
		}
	}

	protected override void Rebuild()
	{
		AddSpawnlistOptions();

		AddHeader( "#spawnmenu.section.workshop" );
		AddOption( "apps", "#spawnmenu.props.all", () => new SpawnPageCloud { IncludeLocalProps = true } );
		AddOption( "person", "#spawnmenu.props.humans", () => new SpawnPageCloud() { Category = "human" } );
		AddOption( "park", "#spawnmenu.props.nature", () => new SpawnPageCloud() { Category = "nature" } );
		AddOption( "chair", "#spawnmenu.props.furniture", () => new SpawnPageCloud() { Category = "furniture" } );
		AddOption( "pets", "#spawnmenu.props.animal", () => new SpawnPageCloud() { Category = "animal" } );
		AddOption( "category", "#spawnmenu.props.props", () => new SpawnPageCloud { Category = "prop", LocalCategory = "props" } );
		AddOption( "toys", "#spawnmenu.props.toy", () => new SpawnPageCloud() { Category = "toy" } );
		AddOption( "restaurant", "#spawnmenu.props.food", () => new SpawnPageCloud() { Category = "food" } );
		AddOption( "gps_fixed", "#spawnmenu.props.guns", () => new SpawnPageCloud() { Category = "weapon" } );

		AddHeader( "#spawnmenu.section.spawnlists_workshop" );
		AddOption( "trending_up", "#spawnmenu.spawnlist.popular", () => new SpawnlistWorkshop { SortOrder = WorkshopSortMode.Popular } );
		AddOption( "fiber_new", "#spawnmenu.spawnlist.newest", () => new SpawnlistWorkshop { SortOrder = WorkshopSortMode.Newest } );
	}
}
