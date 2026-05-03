
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "#spawnmenu.tab.props" ), Order( 0 ), Icon( "📦" )]
public class PropsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddHeader( "#spawnmenu.section.workshop" );
		AddOption( "🧠", "#spawnmenu.props.all", () => new SpawnPageCloud() );
		AddOption( "🥸", "#spawnmenu.props.humans", () => new SpawnPageCloud() { Category = "human" } );
		AddOption( "🌲", "#spawnmenu.props.nature", () => new SpawnPageCloud() { Category = "nature" } );
		AddOption( "🪑", "#spawnmenu.props.furniture", () => new SpawnPageCloud() { Category = "furniture" } );
		AddOption( "🐵", "#spawnmenu.props.animal", () => new SpawnPageCloud() { Category = "animal" } );
		AddOption( "🪠", "#spawnmenu.props.prop", () => new SpawnPageCloud() { Category = "prop" } );
		AddOption( "🪀", "#spawnmenu.props.toy", () => new SpawnPageCloud() { Category = "toy" } );
		AddOption( "🍦", "#spawnmenu.props.food", () => new SpawnPageCloud() { Category = "food" } );
		AddOption( "🔫", "#spawnmenu.props.guns", () => new SpawnPageCloud() { Category = "weapon" } );

		AddHeader( "#spawnmenu.section.local" );
		AddOption( "🧍", "#spawnmenu.props.all", () => new SpawnPageLocal() );
		AddOption( "🙎", "#spawnmenu.props.characters", () => new SpawnPageLocal() { Category = "characters" } );
		AddOption( "📦", "#spawnmenu.props.props", () => new SpawnPageLocal() { Category = "props" } );
	}
}
