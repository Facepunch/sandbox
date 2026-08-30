
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
		{ "Vehicle", "🚕" },
		{ "World", "🌍" },
	};

	protected override void Rebuild()
	{
		AddHeader( "#spawnmenu.section.categories" );
		AddOption( "\U0001f9e0", "#spawnmenu.entity.all", () => new EntityListCloud { IncludeLocalEntities = true, ExcludedLocalCategoryRoots = new[] { "Weapon", "Npc" }, Query = "-category:weapon -category:npc" } );

		var categories = ResourceLibrary.GetAll<ScriptedEntity>()
			.Where( e => !e.Developer || ServerSettings.ShowDeveloperEntities )
			.Select( e => SpawnMenuCategory.Root( e.Category ) )
			.Where( category => !string.Equals( category, "Weapon", StringComparison.OrdinalIgnoreCase ) && !string.Equals( category, "Npc", StringComparison.OrdinalIgnoreCase ) )
			.Distinct()
			.OrderBy( c => c == "Other" ? "\xFF" : c ); // sort Other last

		var addedCategories = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var category in categories )
		{
			var cat = category; // capture for lambda
			var icon = CategoryIcons.GetValueOrDefault( cat, "📦" );
			addedCategories.Add( cat );
			AddOption( icon, cat, () => new EntityListCloud { LocalCategory = cat, Query = $"category:{cat.ToLowerInvariant()}" } );
		}

		void AddCloudCategory( string icon, string name, string category )
		{
			if ( !addedCategories.Add( category ) ) return;
			AddOption( icon, name, () => new EntityListCloud { Query = $"category:{category}" } );
		}

		AddCloudCategory( "🐵", "#spawnmenu.entity.animals", "animal" );
		AddCloudCategory( "🥁", "#spawnmenu.entity.audio", "audio" );
		AddCloudCategory( "✨", "#spawnmenu.entity.effect", "effect" );
		AddCloudCategory( "🎈", "#spawnmenu.entity.other", "other" );
		AddCloudCategory( "💪", "#spawnmenu.entity.showcase", "showcase" );
		AddCloudCategory( "🧸", "#spawnmenu.entity.toys_and_fun", "toyfun" );
		AddCloudCategory( "🚚", "#spawnmenu.entity.vehicle", "vehicle" );
		// AddOption( "⭐", "Favourites", () => new EntityListCloud() { Query = "sort:favourite" } );
	}
}
