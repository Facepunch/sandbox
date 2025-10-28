namespace Sandbox;

public static class Storage
{
	public static StorageContent Create( string type )
	{
		return new StorageContent( type );
	}

	public static StorageContent[] GetAll( string type )
	{
		return Sandbox.FileSystem.Data.FindDirectory( $"/storage/{type}/", "*" )
			.Select( x => Load( type, x ) )
			.Where( x => x is not null )
			.ToArray();
	}

	static Dictionary<string, StorageContent> newCache = new();

	static StorageContent Load( string type, string folderName )
	{
		var cacheKey = $"{type}:{folderName}";

		if ( newCache.TryGetValue( cacheKey, out var cached ) )
		{
			return cached;
		}

		try
		{
			var meta = Sandbox.FileSystem.Data.ReadJson<StorageMeta>( $"/storage/{type}/{folderName}/_meta.json" );
			if ( meta is null ) return null;
			if ( meta.Type != type ) return null;

			var content = new StorageContent( meta );
			newCache[cacheKey] = content;
			return content;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e );
			return null;
		}
	}

	internal static void OnDeleted( StorageContent source )
	{
		foreach ( var kv in newCache.Where( x => x.Value == source ).ToArray() )
		{
			newCache.Remove( kv.Key );
		}
	}
}

internal class StorageMeta
{
	public string Id { get; set; }
	public string Type { get; set; }
	public string Schema { get; set; }
	public DateTimeOffset Timestamp { get; set; }
	public Dictionary<string, string> Meta { get; set; }
}
