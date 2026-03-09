using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Top-level spawn menu tab for user-created spawnlists.
/// </summary>
[Title( "Spawnlists" ), Order( 1000 ), Icon( "📋" )]
public class SpawnlistsPage : BaseSpawnMenu
{
	bool _queried;
	List<Storage.Entry> _cloudEntries = new();

	protected override void Rebuild()
	{
		AddOption( "➕", "Create New", () => new SpawnlistCreateDialog( this ) );

		AddHeader( "You" );

		var localEntries = SpawnlistData.GetAll().ToList();
		var seenWorkshopIds = new HashSet<string>();

		// Local (editable) spawnlists first, tracking their workshop IDs
		foreach ( var entry in localEntries.Where( e => !e.Files.IsReadOnly ) )
		{
			var data = SpawnlistData.Load( entry );
			var workshopId = entry.GetMeta( "_workshopId", 0ul );
			if ( workshopId > 0 ) seenWorkshopIds.Add( workshopId.ToString() );
			var capturedEntry = entry;
			var icon = workshopId > 0 ? "🌧️" : "📁";
			AddOption( icon, $"{data.Name}", () => new SpawnlistView { Entry = capturedEntry },
				() => OnSpawnlistRightClick( capturedEntry ) );
		}

		// Cloud entries that were installed from the workshop
		foreach ( var entry in _cloudEntries )
		{
			var workshopId = entry.GetMeta( "_workshopId", 0ul );
			if ( workshopId > 0 && !seenWorkshopIds.Add( workshopId.ToString() ) )
				continue;

			var data = SpawnlistData.Load( entry );
			var capturedEntry = entry;
			AddOption( "☁️", $"{data.Name}", () => new SpawnlistView { Entry = capturedEntry } );
		}

		// Fetch user's own cloud spawnlists
		if ( !_queried )
		{
			_queried = true;
			_ = FetchCloudSpawnlists();
		}

		AddGrow();
		AddHeader( "Workshop" );
		AddOption( "🎖️", "Popular", () => new SpawnlistWorkshop { SortOrder = Storage.SortOrder.RankedByVote } );
		AddOption( "🐣", "Newest", () => new SpawnlistWorkshop { SortOrder = Storage.SortOrder.RankedByPublicationDate } );
	}

	void OnSpawnlistRightClick( Storage.Entry entry )
	{
		var menu = MenuPanel.Open( this );
		menu.AddOption( "delete", "Delete", () =>
		{
			SpawnlistData.Delete( entry );
			RefreshList();
		} );
	}

	/// <summary>
	/// Query the cloud for the current user's published spawnlists
	/// and install them so they appear in the sidebar.
	/// </summary>
	async Task FetchCloudSpawnlists()
	{
		var query = new Storage.Query();
		query.KeyValues["package"] = "facepunch.sandbox";
		query.KeyValues["type"] = "spawnlist";

		// Directed search for current user
		query.Author = Game.SteamId;

		var result = await query.Run();
		if ( result?.Items == null ) return;

		var localWorkshopIds = SpawnlistData.GetAll()
			.Select( e => e.GetMeta( "_workshopId", 0ul ) )
			.Where( id => id > 0 )
			.Select( id => id.ToString() )
			.ToHashSet();

		bool anyNew = false;

		foreach ( var item in result.Items )
		{
			if ( localWorkshopIds.Contains( item.Id.ToString() ) )
				continue;

			var installed = await item.Install();
			if ( installed == null ) continue;

			installed.SetMeta( "_workshopId", item.Id );
			_cloudEntries.Add( installed );
			anyNew = true;
		}

		if ( anyNew )
			OnParametersSet();
	}

	/// <summary>
	/// Call this to refresh the sidebar after creating or deleting a spawnlist.
	/// </summary>
	public void RefreshList()
	{
		_queried = false;
		_cloudEntries.Clear();
		OnParametersSet();
	}
}
