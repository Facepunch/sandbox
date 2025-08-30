
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Games" ), Order( 2000 )]
public class MountsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		foreach ( var entry in Sandbox.Mounting.Directory.GetAll().Where( x => x.Available ).OrderBy( x => x.Title ) )
		{
			AddOption( entry.Title, () => new MountContent() { Ident = entry.Ident } );
		}
	}
}
