
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "#spawnmenu.tab.spawnlists" ), Order( 0 ), Icon( "📦" )]
public class PropsPage : SpawnlistsPage
{
	protected override void Rebuild()
	{
		AddHeader( "#spawnmenu.section.props" );
		AddOption( "🧠", "#spawnmenu.props.all", () => new SpawnPageCloud { IncludeLocalProps = true } );
		AddOption( "🥸", "#spawnmenu.props.humans", () => new SpawnPageCloud() { Category = "human" } );
		AddOption( "🌲", "#spawnmenu.props.nature", () => new SpawnPageCloud() { Category = "nature" } );
		AddOption( "🪑", "#spawnmenu.props.furniture", () => new SpawnPageCloud() { Category = "furniture" } );
		AddOption( "🐵", "#spawnmenu.props.animal", () => new SpawnPageCloud() { Category = "animal" } );
		AddOption( "🪠", "#spawnmenu.props.props", () => new SpawnPageCloud { Category = "prop", LocalCategory = "props" } );
		AddOption( "🪀", "#spawnmenu.props.toy", () => new SpawnPageCloud() { Category = "toy" } );
		AddOption( "🍦", "#spawnmenu.props.food", () => new SpawnPageCloud() { Category = "food" } );
		AddOption( "🔫", "#spawnmenu.props.guns", () => new SpawnPageCloud() { Category = "weapon" } );

		AddOption( "🙎", "#spawnmenu.props.characters", () => new SpawnPageCloud { Category = "characters" } );

		AddSpawnlistOptions();
	}
}
