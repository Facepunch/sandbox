[Title( "#spawnmenu.tab.weapons" ), Order( 2010 ), Icon( "🔫" )]
public class WeaponsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddHeader( "#spawnmenu.section.categories" );
		AddOption( "📦", "#spawnmenu.entity.all", () => new EntityListCloud { LocalCategory = "Weapon", Query = "category:weapon" } );
		AddOption( "🗡️", "#spawnmenu.weapon.melee", () => new EntityListCloud { LocalCategory = "Weapon/Melee", Query = "category:weapon/melee" } );
		AddOption( "🛠️", "#spawnmenu.weapon.utility", () => new EntityListCloud { LocalCategory = "Weapon/Utility", Query = "category:weapon/utility" } );
		AddOption( "🔫", "#spawnmenu.weapon.weapons", () => new EntityListCloud { LocalCategory = "Weapon", IncludeLocalSubcategories = false, Query = "category:weapon" } );
	}
}
