
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "#spawnmenu.tab.entity" ), Order( 2000 ), Icon( "🧠" )]
public class EntityPage : BaseSpawnMenu
{
	static Dictionary<string, string> CategoryIcons = new()
	{
		{ "Chair", "🪑" },
		{ "Pickup", "🧰" },
		{ "Weapon", "🔫" },
		{ "Npc", "🤖" },
		{ "Vehicle", "🚕" },
		{ "World", "🌍" },
	};

	protected override void Rebuild()
	{
		AddHeader( "#spawnmenu.section.local" );

		var categories = ResourceLibrary.GetAll<ScriptedEntity>()
			.Where( e => !e.Developer || ServerSettings.ShowDeveloperEntities )
			.Select( e => string.IsNullOrWhiteSpace( e.Category ) ? "Other" : e.Category )
			.Distinct()
			.OrderBy( c => c == "Other" ? "\xFF" : c ); // sort Other last

		foreach ( var category in categories )
		{
			var cat = category; // capture for lambda
			var icon = CategoryIcons.GetValueOrDefault( cat, "📦" );
			AddOption( icon, cat, () => new EntityListLocal { Category = cat } );
		}

		AddHeader( "#spawnmenu.section.workshop" );
		AddOption( "\U0001f9e0", "#spawnmenu.entity.all", () => new EntityListCloud() { Query = "" } );
		AddOption( "🐵", "#spawnmenu.entity.animals", () => new EntityListCloud() { Query = "cat:animal" } );
		AddOption( "🥁", "#spawnmenu.entity.audio", () => new EntityListCloud() { Query = "cat:audio" } );
		AddOption( "✨", "#spawnmenu.entity.effect", () => new EntityListCloud() { Query = "cat:effect" } );
		AddOption( "🥼", "#spawnmenu.entity.npc", () => new EntityListCloud() { Query = "cat:npc" } );
		AddOption( "🎈", "#spawnmenu.entity.other", () => new EntityListCloud() { Query = "cat:other" } );
		AddOption( "💪", "#spawnmenu.entity.showcase", () => new EntityListCloud() { Query = "cat:showcase" } );
		AddOption( "🧸", "#spawnmenu.entity.toys_and_fun", () => new EntityListCloud() { Query = "cat:toyfun" } );
		AddOption( "🚚", "#spawnmenu.entity.vehicle", () => new EntityListCloud() { Query = "cat:vehicle" } );
		// AddOption( "⭐", "Favourites", () => new EntityListCloud() { Query = "sort:favourite" } );
	}
}
