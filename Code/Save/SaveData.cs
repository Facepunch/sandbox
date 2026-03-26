namespace Sandbox;

/// <summary>
/// Handles CRUD operations for save game <see cref="Storage.Entry"/> objects.
/// </summary>
public static class SaveData
{
	public const string EntryType = "save";

	/// <summary>
	/// Create a new storage entry and populate its metadata and thumbnail.
	/// The caller is responsible for writing content via <see cref="SaveSystem.SaveToEntry"/>.
	/// </summary>
	public static Storage.Entry Create( string title, string timestamp, string mapIdent, Bitmap thumbnail = null )
	{
		var entry = Storage.CreateEntry( EntryType );
		SetMetadata( entry, title, timestamp, mapIdent, thumbnail );
		return entry;
	}

	/// <summary>
	/// Update the metadata and optional thumbnail on an existing entry.
	/// </summary>
	public static void SetMetadata( Storage.Entry entry, string title, string timestamp, string mapIdent, Bitmap thumbnail = null )
	{
		entry.SetMeta( "title", title );
		entry.SetMeta( "timestamp", timestamp );
		entry.SetMeta( "map", mapIdent ?? "" );
		if ( thumbnail is not null )
			entry.SetThumbnail( thumbnail );
	}

	/// <summary>
	/// Returns all local saves, optionally filtered to only those matching <paramref name="mapIdent"/>.
	/// </summary>
	public static IEnumerable<Storage.Entry> GetAll( string mapIdent = null )
	{
		var all = Storage.GetAll( EntryType ).OrderByDescending( x => x.Created );
		if ( mapIdent is null )
			return all;
		return all.Where( e => e.GetMeta<string>( "map" ) == mapIdent );
	}

	public static string GetTitle( Storage.Entry entry ) => entry.GetMeta<string>( "title" ) ?? "Untitled";
	public static string GetTimestamp( Storage.Entry entry ) => entry.GetMeta<string>( "timestamp" ) ?? "";
	public static string GetMap( Storage.Entry entry ) => entry.GetMeta<string>( "map" ) ?? "";
	public static void Delete( Storage.Entry entry ) => entry.Delete();

	public static void Publish( Storage.Entry entry )
	{
		var options = new Modals.WorkshopPublishOptions { Title = GetTitle( entry ) };
		options.KeyValues["map"] = GetMap( entry );
		entry.Publish( options );
	}
}
