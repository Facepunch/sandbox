/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Games" ), Order( 2000 ), Icon( "🧩" )]
public class MountsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		var all = Sandbox.Mounting.Directory.GetAll().ToArray();
		var available = all.Where( x => x.Available ).ToArray();
		var unavailable = all.Where( x => !x.Available ).ToArray();

		if ( available.Any() )
		{
			AddHeader( "Local" );

			foreach ( var entry in available.OrderBy( x => x.Title ) )
			{
				AddOption( entry.Title, () => new MountContent() { Ident = entry.Ident } );
			}
		}

		if ( unavailable.Any() )
		{
			AddHeader( "Not Installed" );
			
			foreach ( var entry in unavailable.OrderBy( x => x.Title ) )
			{
				AddOption( entry.Title, null );
			}
		}
	}
}
