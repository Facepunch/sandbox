
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Entity" ), Order( 2000 ), Icon( "🧠" )]
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
		AddHeader( "Local" );

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
		// AddOption( "⭐", "Favourites", () => new EntityListCloud() { Query = "sort:favourite" } );
	}
}
