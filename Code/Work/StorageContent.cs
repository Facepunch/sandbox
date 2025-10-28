using Sandbox.Modals;
using System.Text.Json.Nodes;

namespace Sandbox;

public sealed class StorageContent
{
	public string Id { get; private set; }
	public string Type { get; private set; }
	public string Schema { get; private set; }
	public Dictionary<string, string> Meta { get; } = new();
	public DateTimeOffset Created { get; private set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// This is where you save and load your files to
	/// </summary>
	public BaseFileSystem Files { get; private set; }

	private string DataPath => $"/storage/{Type}/{Id}/";
	private string MetaPath => "_meta.json";
	private string ThumbPath => "_thumb.png";

	public StorageContent( string type )
	{
		Type = type;
		Id = Guid.NewGuid().ToString();

		ValidateType();

		Sandbox.FileSystem.Data.CreateDirectory( DataPath );
		Files = Sandbox.FileSystem.Data.CreateSubSystem( DataPath );

		SaveMeta();
	}

	internal StorageContent( StorageMeta meta )
	{
		Id = meta.Id;
		Type = meta.Type;
		Schema = meta.Schema;
		Meta = meta.Meta?.ToDictionary() ?? this.Meta;
		Created = meta.Timestamp;

		ValidateType();

		Sandbox.FileSystem.Data.CreateDirectory( DataPath );
		Files = Sandbox.FileSystem.Data.CreateSubSystem( DataPath );
	}

	private void ValidateType()
	{
		ArgumentNullException.ThrowIfNull( Type );

		if ( Type.Length < 1 ) throw new System.ArgumentException( "type cannot be empty", nameof( Type ) );
		if ( Type.Length > 16 ) throw new System.ArgumentException( "type should be under 16 characters", nameof( Type ) );

		// TODO - validate type and filename

		// type should be letters only, no symbols
		if ( !System.Text.RegularExpressions.Regex.IsMatch( Type, "^[a-zA-Z]+$" ) )
		{
			throw new System.ArgumentException( "Invalid storage type", nameof( Type ) );
		}
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
		SaveMeta();
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

	void SaveMeta()
	{
		var meta = new StorageMeta
		{
			Id = this.Id,
			Type = this.Type,
			Schema = this.Schema,
			Meta = this.Meta,
			Timestamp = this.Created
		};

		Files.WriteJson( MetaPath, meta );
	}

	bool _thumbGenerated = false;
	Texture _thumbnail;

	public Texture Thumbnail
	{
		get
		{
			if ( _thumbGenerated ) return _thumbnail;
			_thumbGenerated = true;
			if ( !Files.FileExists( ThumbPath ) ) return null;

			var data = Files.ReadAllBytes( ThumbPath );
			using var bitmap = Bitmap.CreateFromBytes( data.ToArray() );
			_thumbnail = bitmap?.ToTexture();

			return _thumbnail;
		}
	}


	public void SetThumbnail( Bitmap bitmap )
	{
		_thumbGenerated = false;

		var png = bitmap.ToPng();
		Files.WriteAllBytes( ThumbPath, png );
	}

	public void Delete()
	{
		Files.DeleteDirectory( "/", true );
	}

	public void Publish()
	{
		var meta = Files.ReadAllText( MetaPath );

		var o = new WorkshopPublishOptions
		{
			Title = "Unnammed",
			FileSystem = FileSystem.CreateMemoryFileSystem(),
			KeyValues = new(),
			Tags = [Type],
			Metadata = meta
		};

		// copy everything over
		foreach ( var fn in Files.FindFile( "/", "*", true ) )
		{
			o.FileSystem.WriteAllText( fn, Files.ReadAllText( fn ) );
		}

		// assign the thumbnail
		if ( Files.FileExists( ThumbPath ) )
		{
			var thumbData = Sandbox.FileSystem.Data.ReadAllBytes( ThumbPath );
			var bitmap = Bitmap.CreateFromBytes( thumbData.ToArray() );
			o.Thumbnail = bitmap;
		}

		o.KeyValues["storage"] = Schema;
		o.KeyValues["type"] = Type;
		o.KeyValues["source"] = "storage";

		Game.Overlay.WorkshopPublish( o );
	}
}
