using System.Text.Json.Nodes;

namespace Sandbox;

public static class Storage
{
	public static StorageContent Create( string type, string filename )
	{
		return new StorageContent( type, filename );
	}

	public static StorageContent[] GetAll( string type )
	{
		return Sandbox.FileSystem.Data.FindFile( $"/storage/{type}/", "*.meta" )
			.Select( x => Load( type, x ) )
			.Where( x => x is not null )
			.ToArray();
	}

	static Dictionary<string, StorageContent> cache = new();

	static StorageContent Load( string type, string metaFilename )
	{
		var cacheKey = $"{type}:{metaFilename}";

		if ( cache.TryGetValue( cacheKey, out var cached ) )
		{
			return cached;
		}

		try
		{
			var meta = Sandbox.FileSystem.Data.ReadJson<StorageMeta>( $"/storage/{type}/{metaFilename}" );
			if ( meta is null ) return null;
			if ( meta.Type != type ) return null;

			var content = new StorageContent( meta );
			cache[cacheKey] = content;
			return content;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e );
			return null;
		}
	}
}

internal class StorageMeta
{
	public string Type { get; set; }
	public string Path { get; set; }
	public string Schema { get; set; }
	public DateTime Timestamp { get; set; }
	public Dictionary<string, string> Meta { get; set; }
}

public sealed class StorageContent
{
	public string Type { get; private set; }
	public string Path { get; private set; }
	public string Schema { get; private set; }
	public Dictionary<string, string> Meta { get; } = new();

	public StorageContent( string type, string filename )
	{
		ArgumentNullException.ThrowIfNull( type );
		if ( type.Length < 1 ) throw new System.ArgumentException( "type cannot be empty", nameof( type ) );
		if ( type.Length > 16 ) throw new System.ArgumentException( "type should be under 16 characters", nameof( type ) );

		ArgumentNullException.ThrowIfNull( filename );
		if ( filename.Length < 1 ) throw new System.ArgumentException( "filename cannot be empty", nameof( filename ) );
		if ( filename.Length > 128 ) throw new System.ArgumentException( "filename should be under 128 characters", nameof( filename ) );

		// TODO - validate type and filename

		// type should be letters only, no symbols
		if ( !System.Text.RegularExpressions.Regex.IsMatch( type, "^[a-zA-Z]+$" ) )
		{
			throw new System.ArgumentException( "Invalid storage type", nameof( type ) );
		}

		// filename should have no special symbols, only letters, numbers, underscores
		if ( !System.Text.RegularExpressions.Regex.IsMatch( filename, "^[a-zA-Z0-9_-]+$" ) )
		{
			throw new System.ArgumentException( "Invalid storage filename", nameof( filename ) );
		}

		this.Type = type;
		this.Path = filename;
	}

	internal StorageContent( StorageMeta meta )
	{
		this.Type = meta.Type;
		this.Path = meta.Path;
		this.Schema = meta.Schema;
		this.Meta = meta.Meta?.ToDictionary() ?? this.Meta;
	}

	string GenerateFilename()
	{
		return $"/storage/{Type}/{Path}";
	}

	/// <summary>
	/// Set a meta value
	/// </summary>
	public void SetMeta<T>( string key, T value )
	{
		if ( value == null )
		{
			Meta.Remove( key );
			return;
		}

		Meta[key] = JsonValue.Create( value )?.ToJsonString();
	}

	/// <summary>
	/// Get a meta value
	/// </summary>
	public T GetMeta<T>( string key, T defaultValue = default )
	{
		if ( Meta.TryGetValue( key, out var val ) )
		{
			return Json.Deserialize<T>( val );
		}

		return defaultValue;
	}

	/// <summary>
	/// Write data as JSON
	/// </summary>
	public void WriteAsJson<T>( T data )
	{
		var fn = GenerateFilename();
		Sandbox.FileSystem.Data.WriteJson( fn, data );
		Schema = "json";
		SaveMeta( fn );
	}

	/// <summary>
	/// Writes the string data to a file
	/// </summary>
	public void WriteAsString( string data )
	{
		var fn = GenerateFilename();
		Sandbox.FileSystem.Data.WriteAllText( fn, data );
		Schema = "string";
		SaveMeta( fn );
	}

	/// <summary>
	/// Reads the string data from a file
	/// </summary>
	public string ReadString()
	{
		var fn = GenerateFilename();
		return Sandbox.FileSystem.Data.ReadAllText( fn );
	}

	/// <summary>
	/// Writes the byte data to a file
	/// </summary>
	public void WriteAsBytes( byte[] data )
	{
		var fn = GenerateFilename();
		using var s = Sandbox.FileSystem.Data.OpenWrite( fn, System.IO.FileMode.Create );
		s.Write( data, 0, data.Length );
		Schema = "bytes";
		SaveMeta( fn );
	}

	void SaveMeta( string filename )
	{
		var metaFn = filename + ".meta";
		var meta = new StorageMeta
		{
			Type = this.Type,
			Path = this.Path,
			Schema = this.Schema,
			Meta = this.Meta,
			Timestamp = DateTime.UtcNow
		};
		Sandbox.FileSystem.Data.WriteJson( metaFn, meta );
	}


}
