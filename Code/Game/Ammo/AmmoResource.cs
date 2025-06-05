[GameResource( "Ammo Resource", "ammo", "Ammo", Icon = "📦", IconBgColor = "#f54248" )]
public class AmmoResource : GameResource
{
	/// <summary>
	/// The type of ammo this resource represents
	/// </summary>
	[Property, Group( "Ammo" )]
	public string AmmoType { get; set; }

	/// <summary>
	/// The maximum amount of ammo that can be held
	/// </summary>
	[Property, Group( "Ammo" )]
	public int MaxAmount { get; set; }
}
