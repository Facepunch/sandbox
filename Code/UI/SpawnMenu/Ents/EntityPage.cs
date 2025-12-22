
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Entity" ), Order( 2000 ), Icon( "🧠" )]
public class EntityPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddHeader( "You" );
		AddOption( "📂", "Installed", () => new EntityListLocal() { } );

		AddHeader( "Workshop" );
		AddOption( "\U0001f9e0", "All", () => new EntityListCloud() { Query = "" } );
		AddOption( "🐵", "Animals", () => new EntityListCloud() { Query = "cat:animal" } );
		AddOption( "🥁", "Audio", () => new EntityListCloud() { Query = "cat:audio" } );
		AddOption( "✨", "Effect", () => new EntityListCloud() { Query = "cat:effect" } );
		AddOption( "🥼", "Npc", () => new EntityListCloud() { Query = "cat:npc" } );
		AddOption( "🎈", "Other", () => new EntityListCloud() { Query = "cat:other" } );
		AddOption( "💪", "Showcase", () => new EntityListCloud() { Query = "cat:showcase" } );
		AddOption( "🧸", "Toys & Fun", () => new EntityListCloud() { Query = "cat:toy" } );
		AddOption( "🚚", "Vehicle", () => new EntityListCloud() { Query = "cat:vehicle" } );
		AddOption( "⭐", "Favourites", () => new EntityListCloud() { Query = "sort:favourite" } );
	}
}
