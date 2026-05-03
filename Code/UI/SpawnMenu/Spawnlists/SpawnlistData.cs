namespace Sandbox;

using Sandbox.UI;
using System.Text.Json.Serialization;

/// <summary>
/// A spawnlist item -- lots of cleanup needed, docs, etc
/// </summary>
public class SpawnlistItem
{
	[JsonPropertyName( "ident" )]
	public string Ident { get; set; }

	[JsonPropertyName( "title" )]
	public string Title { get; set; }

	[JsonPropertyName( "icon" )]
	public string Icon { get; set; }

	public static string MakeIdent( string type, string path, string source = "local" )
	{
		// TODO: hate this special case
		if ( type == "dupe" )
			return $"dupe.{source}:{path}";

		return $"{type}:{path}";
	}

	public static (string Type, string Path, string Source) ParseIdent( string ident )
	{
		if ( string.IsNullOrEmpty( ident ) )
			return (null, null, "local");

		var colonIndex = ident.IndexOf( ':' );
		if ( colonIndex < 0 )
			return (ident, ident, "local");

		var prefix = ident[..colonIndex];
		var data = ident[(colonIndex + 1)..];

		// TODO: hate this special case
		if ( prefix.StartsWith( "dupe." ) )
		{
			var source = prefix["dupe.".Length..];
			return ("dupe", data, source);
		}

		return (prefix, data, "local");
	}
}

public class SpawnlistData
{
	/// <summary>
	/// Raised whenever a new spawnlist is created, so UI can refresh without needing a panel ancestor walk.
	/// </summary>
	public static event Action SpawnlistCreated;

	[JsonPropertyName( "name" )]
	public string Name { get; set; } = "#spawnmenu.spawnlist.untitled";

	[JsonPropertyName( "description" )]
	public string Description { get; set; } = "";

	[JsonPropertyName( "items" )]
	public List<SpawnlistItem> Items { get; set; } = new();

	public static SpawnlistData Create( string name )
	{
		var data = new SpawnlistData { Name = name };
		var entry = Storage.CreateEntry( "spawnlist" );
		entry.SetMeta( "name", name );
		Save( entry, data );
		SpawnlistCreated?.Invoke();
		return data;
	}

	public static void Save( Storage.Entry entry, SpawnlistData data )
	{
		entry.Files.WriteJson( "/spawnlist.json", data );
		entry.SetMeta( "name", data.Name );
		entry.SetMeta( "item_count", data.Items.Count.ToString() );
	}

	public static SpawnlistData Load( Storage.Entry entry )
	{
		if ( !entry.Files.FileExists( "/spawnlist.json" ) )
			return new SpawnlistData { Name = entry.GetMeta<string>( "name" ) ?? "#spawnmenu.spawnlist.untitled" };

		return entry.Files.ReadJson<SpawnlistData>( "/spawnlist.json" )
			?? new SpawnlistData { Name = "#spawnmenu.spawnlist.untitled" };
	}

	public static IEnumerable<Storage.Entry> GetAll()
	{
		return Storage.GetAll( "spawnlist" ).OrderByDescending( x => x.Created );
	}

	public static void Rename( Storage.Entry entry, string newName )
	{
		var data = Load( entry );
		data.Name = newName;
		Save( entry, data );
	}

	public static void Delete( Storage.Entry entry )
	{
		entry.Delete();
	}

	public static void Publish( Storage.Entry entry )
	{
		var options = new Modals.WorkshopPublishOptions { Title = "#spawnmenu.spawnlist.publish_title" };
		entry.Publish( options );
	}

	public static void AddItem( Storage.Entry entry, SpawnlistItem item )
	{
		var data = Load( entry );
		data.Items.Add( item );
		Save( entry, data );
	}

	public static void RemoveItem( Storage.Entry entry, int index )
	{
		var data = Load( entry );
		if ( index >= 0 && index < data.Items.Count )
		{
			data.Items.RemoveAt( index );
			Save( entry, data );
		}
	}

	public static void PopulateContextMenu( MenuPanel menu, SpawnlistItem item, Storage.Entry skipEntry = null )
	{
		var entries = GetAll()
			.Where( e => skipEntry is null || e.Id != skipEntry.Id )
			.ToList();

		if ( entries.Count > 0 )
		{
			menu.AddSubmenu( "📋", "#spawnmenu.spawnlist.add_to_submenu", sub =>
			{
				foreach ( var entry in entries )
				{
					var data = Load( entry );
					var capturedEntry = entry;
					sub.AddOption( "📋", data.Name, () => AddItem( capturedEntry, item ) );
				}
			} );

			menu.AddSpacer();
		}

		menu.AddOption( "➕", "#spawnmenu.spawnlist.create_new_option", () =>
		{
			Create( item.Title ?? "New Spawnlist" );
			var created = GetAll().FirstOrDefault();
			if ( created is not null )
				AddItem( created, item );
		} );
	}
}
