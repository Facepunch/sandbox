
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Props" ), Order( 0 )]
public class PropsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddOption( "Favourites", () => new SpawnPageFavourites() );
		AddOption( "Humans", () => new SpawnPageCloud() { Category = "human" } );
		AddOption( "Nature", () => new SpawnPageCloud() { Category = "nature" } );
		AddOption( "Furniture", () => new SpawnPageCloud() { Category = "furniture" } );
		AddOption( "Animal", () => new SpawnPageCloud() { Category = "animal" } );
		AddOption( "Prop", () => new SpawnPageCloud() { Category = "prop" } );
		AddOption( "Toy", () => new SpawnPageCloud() { Category = "toy" } );
		AddOption( "Food", () => new SpawnPageCloud() { Category = "food" } );
	}
}
