namespace Sandbox;

/// <summary>
/// Manages the list of save game entries for the current map — local editable saves and cloud-installed ones.
/// Mirrors the pattern used by <see cref="SpawnlistCollection"/>.
/// </summary>
public class SaveCollection
{
	public record Entry(
		string Icon,
		string Title,
		string Timestamp,
		Storage.Entry StorageEntry,
		ulong WorkshopId,
		bool IsEditable
	);

	/// <summary>
	/// Raised whenever the visible entry list changes.
	/// </summary>
	public event Action Changed;

	/// <summary>
	/// Raised after a workshop save is installed, with the save title.
	/// </summary>
	public event Action<string> Installed;

	/// <summary>
	/// Raised after a workshop save is uninstalled.
	/// </summary>
	public event Action Uninstalled;

	public IReadOnlyList<Entry> Entries => _entries;

	public int PendingCount
	{
		get
		{
			if ( !_loading ) return 0;
			var loaded = _entries.Select( e => e.WorkshopId ).Where( id => id > 0 ).ToHashSet();
			return new SavedInstalls().Installed.Count( id => !loaded.Contains( id ) );
		}
	}

	List<Entry> _entries = new();
	Dictionary<ulong, Storage.Entry> _cloudEntries = new();
	bool _queried;
	bool _loading;

	public void Refresh()
	{
		_queried = false;
		Rebuild();
	}

	public async Task InstallAsync( Storage.QueryItem item )
	{
		var entry = await item.Install();
		if ( entry is null ) return;

		var saved = new SavedInstalls();
		saved.Add( item.Id );
		saved.Save();

		Installed?.Invoke( item.Title );
		Refresh();
	}

	public void Uninstall( ulong workshopId )
	{
		if ( workshopId == 0 ) return;

		var saved = new SavedInstalls();
		if ( saved.Remove( workshopId ) )
			saved.Save();

		_cloudEntries.Remove( workshopId );
		Uninstalled?.Invoke();
		Rebuild();
	}

	public void Delete( Storage.Entry entry )
	{
		try
		{
			SaveData.Delete( entry );
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"SaveCollection: Failed to delete entry: {e.Message}" );
		}
		finally
		{
			Refresh();
		}
	}

	struct SavedInstalls
	{
		public List<ulong> Installed { get; set; }

		public SavedInstalls()
		{
			Installed = Game.Cookies.Get<List<ulong>>( "saves.installed", new() );
		}

		public void Save() => Game.Cookies.Set( "saves.installed", Installed );
		public void Add( ulong id ) { if ( !Installed.Contains( id ) ) Installed.Add( id ); }
		public bool Remove( ulong id ) => Installed.Remove( id );
		public HashSet<ulong> ToHashSet() => Installed.ToHashSet();
	}

	void Rebuild()
	{
		var mapIdent = SaveSystem.Current?.MapIdent;
		var installedIds = new SavedInstalls().ToHashSet();
		var result = new List<Entry>();

		foreach ( var storageEntry in SaveData.GetAll( mapIdent ) )
		{
			try
			{
				if ( storageEntry.Files.IsReadOnly ) continue;
			}
			catch { continue; }

			result.Add( new Entry(
				"💾",
				SaveData.GetTitle( storageEntry ),
				SaveData.GetTimestamp( storageEntry ),
				storageEntry,
				0,
				true
			) );
		}

		foreach ( var (workshopId, storageEntry) in _cloudEntries )
		{
			if ( !installedIds.Contains( workshopId ) ) continue;
			if ( SaveData.GetMap( storageEntry ) != mapIdent ) continue;

			result.Add( new Entry(
				"☁️",
				SaveData.GetTitle( storageEntry ),
				SaveData.GetTimestamp( storageEntry ),
				storageEntry,
				workshopId,
				false
			) );
		}

		_entries = result;

		if ( !_queried ) _loading = true;

		Changed?.Invoke();

		if ( !_queried )
		{
			_queried = true;
			_ = FetchCloudSaves( mapIdent );
		}
	}

	async Task FetchCloudSaves( string mapIdent )
	{
		var query = new Storage.Query();
		query.KeyValues["package"] = "facepunch.sandbox";
		query.KeyValues["type"] = SaveData.EntryType;
		if ( !string.IsNullOrEmpty( mapIdent ) )
			query.KeyValues["map"] = mapIdent;
		query.Author = Game.SteamId;

		var result = await query.Run();

		if ( result?.Items is not null )
		{
			foreach ( var item in result.Items )
			{
				if ( _cloudEntries.ContainsKey( item.Id ) ) continue;

				var installed = await item.Install();
				if ( installed == null ) continue;

				_cloudEntries[item.Id] = installed;
			}
		}

		var missingIds = new SavedInstalls().Installed
			.Where( id => !_cloudEntries.ContainsKey( id ) )
			.ToList();

		if ( missingIds.Count > 0 )
		{
			var missingResult = await new Storage.Query { FileIds = missingIds }.Run();
			if ( missingResult?.Items is not null )
			{
				foreach ( var item in missingResult.Items )
				{
					if ( _cloudEntries.ContainsKey( item.Id ) ) continue;

					var installed = await item.Install();
					if ( installed == null ) continue;

					_cloudEntries[item.Id] = installed;
				}
			}
		}

		_loading = false;
		Rebuild();
	}
}
