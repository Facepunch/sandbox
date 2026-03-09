public enum PostProcessGroup
{
	Effects,
	Overlay,
	Shaders,
	Textures,
	Misc
}

[AssetType( Name = "Post Process Effect", Extension = "spp", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class PostProcessResource : GameResource, IDefinitionResource
{
	[Property]
	public PrefabFile Prefab { get; set; }

	[Property]
	public PostProcessGroup Group { get; set; } = PostProcessGroup.Misc;

	[Property]
	public Texture Icon { get; set; }

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	public override Bitmap RenderThumbnail( ThumbnailOptions options )
	{
		if ( Icon is null ) return default;

		return Icon.GetBitmap( 0 );
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "🎨", width, height, "#35B851" );
	}
}

