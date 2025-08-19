[GameResource( "Sandbox Entity", "sent", "An entity that is spawnable from the spawn menu", CanEmbed = false, Icon = "📦", IconBgColor = "#f54248" )]
public class ScriptedEntity : GameResource
{
	[Property]
	public PrefabFile Prefab { get; set; }

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	[Property]
	public Texture Icon { get; set; }

}
