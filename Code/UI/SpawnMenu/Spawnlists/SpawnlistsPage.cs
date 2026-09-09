using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Shared spawnlist support for spawn menu pages.
/// </summary>
public abstract class SpawnlistsPage : BaseSpawnMenu
{
	public SpawnlistCollection Collection { get; } = new();
	readonly Dictionary<string, SpawnMenuOption> _spawnlistOptions = new();

	public SpawnlistsPage()
	{
		BindCollection();
		SpawnlistData.SpawnlistCreated += Collection.Refresh;
		Collection.Refresh();
	}

	void BindCollection()
	{
		Collection.OnChanged = OnCollectionChanged;
		Collection.OnInstalled = OnCollectionInstalled;
	}

	public override void OnHotloaded()
	{
		// Constructors do not run again for existing panels during hotload.
		BindCollection();
		base.OnHotloaded();
	}

	void OnCollectionChanged()
	{
		OnParametersSet();

		// A removed list must stop refreshing its deleted storage and leave the
		// content area on a valid page, whether removed here or from the sidebar.
		if ( ActivePanel is SpawnlistView view &&
			!Collection.Entries.Any( entry => entry.StorageEntry.Id == view.Entry.Id ) )
		{
			DeselectOption();
			view.Delete( true );
			SelectOption( "#spawnmenu.props.all" );
		}
	}

	void OnCollectionInstalled( string name )
	{
		OnParametersSet();
		SelectOption( name );
	}

	public override void OnDeleted()
	{
		SpawnlistData.SpawnlistCreated -= Collection.Refresh;
		Collection.OnChanged = null;
		Collection.OnInstalled = null;
		base.OnDeleted();
	}

	protected void AddSpawnlistOptions()
	{
		_spawnlistOptions.Clear();

		if ( Collection.Entries.Count > 0 || Collection.PendingCount > 0 )
		{
			AddHeader( "#spawnmenu.section.workshop_spawnlists" );

			foreach ( var entry in Collection.Entries )
			{
				var captured = entry;
				_spawnlistOptions[entry.StorageEntry.Id] = AddOption( entry.Icon, entry.Name,
					() => new SpawnlistView { Entry = captured.StorageEntry },
					entry.IsEditable
						? () => OnEditableRightClick( captured )
						: () => OnInstalledRightClick( captured ) );
			}

			AddSkeletons( Collection.PendingCount );
		}

		AddHeader( "#spawnmenu.section.workshop" );
		AddOption( "🎖️", "#spawnmenu.spawnlist.popular", () => new SpawnlistWorkshop { SortOrder = WorkshopSortMode.Popular } );
		AddOption( "🐣", "#spawnmenu.spawnlist.newest", () => new SpawnlistWorkshop { SortOrder = WorkshopSortMode.Newest } );
	}

	protected override void OnMenuFooter( Panel footer )
	{
		footer.AddChild<SpawnlistFooter>();
	}

	/// <summary>Refresh after external changes (create, etc.).</summary>
	public void RefreshList() => Collection.Refresh();

	public void SelectSpawnlist( Storage.Entry entry )
	{
		if ( !_spawnlistOptions.TryGetValue( entry.Id, out var option ) ) return;
		_firstViewed = true;
		SwitchOption( option );
	}

	void OnEditableRightClick( SpawnlistCollection.Entry entry )
	{
		var menu = new Sandbox.UI.Menu();

		if ( entry.StorageEntry.GetMeta( "_workshopId", 0ul ) == 0 )
		{
			menu.AddOption( "#spawnmenu.spawnlist.rename", "edit", () =>
			{
				var data = SpawnlistData.Load( entry.StorageEntry );
				var popup = new StringQueryPopup
				{
					Title = "#spawnmenu.spawnlist.rename_title",
					Prompt = "#spawnmenu.spawnlist.rename_prompt",
					Placeholder = "#spawnmenu.spawnlist.name_placeholder",
					ConfirmLabel = "#spawnmenu.spawnlist.rename_button",
					InitialValue = data.Name,
					OnConfirm = newName =>
					{
						if ( entry.StorageEntry.GetMeta( "_workshopId", 0ul ) != 0 ) return;
						SpawnlistData.Rename( entry.StorageEntry, newName );
						Collection.Refresh();
					}
				};
				popup.Parent = FindPopupPanel();
			} );
		}

		menu.AddOption( "#spawnmenu.spawnlist.delete", "delete", () => Collection.Delete( entry.StorageEntry ) );

		menu.Open( this, Popup.PositionMode.UnderMouse );
	}

	void OnInstalledRightClick( SpawnlistCollection.Entry entry )
	{
		var menu = new Sandbox.UI.Menu();
		menu.AddOption( "#spawnmenu.spawnlist.remove", "delete", () => Collection.Uninstall( entry.WorkshopId ) );

		menu.Open( this, Popup.PositionMode.UnderMouse );
	}
}

