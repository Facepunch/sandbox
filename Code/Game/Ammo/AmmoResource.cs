
[AssetType( Name = "Ammo Type", Extension = "ammo", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding )]
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

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "📦", width, height, "#f54248" );
	}
}
