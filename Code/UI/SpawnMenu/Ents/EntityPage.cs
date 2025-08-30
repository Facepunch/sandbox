
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Entity" ), Order( 2000 )]
public class EntityPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddOption( "All", () => new EntityListCloud() { Query = "sort:newest" } );
		AddOption( "Toy", () => new EntityListCloud() { Query = "cat:toy" } );
		AddOption( "Effect", () => new EntityListCloud() { Query = "cat:effect" } );
		AddOption( "Favourites", () => new EntityListCloud() { Query = "sort:favourite" } );

		if ( Application.IsEditor )
		{
			AddOption( "Local", () => new EntityListLocal() { } );
		}
	}
}
