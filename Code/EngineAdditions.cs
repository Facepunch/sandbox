namespace Sandbox;

public interface IDefinitionResource
{
	public string Title { get; set; }
	public string Description { get; set; }
}

public static class EngineAdditions
{
	extension( AssetTypeAttribute game )
	{
		public static TypeDescription FindTypeByExtension( string extension )
		{
			foreach ( var t in TypeLibrary.GetTypesWithAttribute<AssetTypeAttribute>() )
			{
				if ( string.Equals( t.Attribute.Extension, extension, StringComparison.OrdinalIgnoreCase ) )
					return t.Type;
			}

			return null;
		}
	}
}
