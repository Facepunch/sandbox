/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Dupes" ), Order( 3000 ), Icon( "✌️" )]
public class DupesPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddOption( "Popular Dupes", () => new DupesWorkshop() );
		AddOption( "Local Dupes", () => new DupesLocal() );
	}
}
