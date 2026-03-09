namespace Sandbox;

[Title( "Post Process" ), Order( 8000 ), Icon( "🎨" )]
public class PostProcessPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddHeader( "Installed" );

		var groups = ResourceLibrary.GetAll<PostProcessResource>()
			.Select( r => r.Group )
			.Distinct()
			.OrderBy( g => g );

		foreach ( var group in groups )
		{
			AddOption( "🎨", group.ToString(), () => new PostProcessList { Group = group } );
		}

		AddHeader( "Workshop" );
		AddOption( "🌐", "All", () => new PostProcessListCloud() );
	}
}
