[Title( "#spawnmenu.tab.weapons" ), Order( 2010 ), Icon( "🔫" )]
public class WeaponsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddHeader( "#spawnmenu.section.local" );
		AddOption( "📦", "#spawnmenu.entity.all", () => new EntityListLocal { Category = "Weapon" } );
		AddOption( "🗡️", "#spawnmenu.weapon.melee", () => new EntityListLocal { Category = "Weapon/Melee" } );
		AddOption( "🛠️", "#spawnmenu.weapon.utility", () => new EntityListLocal { Category = "Weapon/Utility" } );
		AddOption( "🔫", "#spawnmenu.weapon.weapons", () => new EntityListLocal { Category = "Weapon", IncludeSubcategories = false } );

		AddHeader( "#spawnmenu.section.workshop" );
		AddOption( "📦", "#spawnmenu.entity.all", () => new EntityListCloud { IncludedCategoryRoot = "Weapon" } );
		AddOption( "🗡️", "#spawnmenu.weapon.melee", () => new EntityListCloud { Query = "cat:weapon/melee" } );
		AddOption( "🛠️", "#spawnmenu.weapon.utility", () => new EntityListCloud { Query = "cat:weapon/utility" } );
		AddOption( "🔫", "#spawnmenu.weapon.weapons", () => new EntityListCloud { Query = "cat:weapon" } );
	}
}
