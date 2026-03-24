using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Top-level spawn menu tab for user-created spawnlists.
/// </summary>
[Title( "Spawnlists" ), Order( 1000 ), Icon( "📋" )]
public class SpawnlistsPage : BaseSpawnMenu
{
	public SpawnlistCollection Collection { get; } = new();

	public SpawnlistsPage()
	{
		Collection.Changed += OnParametersSet;
		Collection.Installed += name =>
		{
			OnParametersSet();
			SelectOption( name );
		};
		Collection.Uninstalled += () =>
		{
			DeselectOption();
			OnParametersSet();
		};
		Collection.Refresh();
	}

	protected override void Rebuild()
	{
		AddHeader( "You" );

		foreach ( var entry in Collection.Entries )
		{
			var captured = entry;
			AddOption( entry.Icon, entry.Name,
				() => new SpawnlistView { Entry = captured.StorageEntry },
				entry.IsEditable
					? () => OnEditableRightClick( captured )
					: () => OnInstalledRightClick( captured ) );
		}

		AddSkeletons( Collection.PendingCount );

		AddGrow();
		AddHeader( "Workshop" );
		AddOption( "🎖️", "Popular", () => new SpawnlistWorkshop { SortOrder = Storage.SortOrder.RankedByVote } );
		AddOption( "🐣", "Newest", () => new SpawnlistWorkshop { SortOrder = Storage.SortOrder.RankedByPublicationDate } );
	}

	protected override void OnMenuFooter( Panel footer )
	{
		footer.AddChild<SpawnlistFooter>();
	}

	/// <summary>Refresh after external changes (create, etc.).</summary>
	public void RefreshList() => Collection.Refresh();

	void OnEditableRightClick( SpawnlistCollection.Entry entry )
	{
		var menu = MenuPanel.Open( this );

		menu.AddOption( "edit", "Rename", () =>
		{
			var data = SpawnlistData.Load( entry.StorageEntry );
			var popup = new StringQueryPopup
			{
				Title = "Rename Spawnlist",
				Prompt = "Enter a new name for your spawnlist.",
				Placeholder = "Spawnlist name...",
				ConfirmLabel = "Rename",
				InitialValue = data.Name,
				OnConfirm = newName =>
				{
					SpawnlistData.Rename( entry.StorageEntry, newName );
					Collection.Refresh();
				}
			};
			popup.Parent = FindPopupPanel();
		} );

		menu.AddOption( "delete", "Delete", () => Collection.Delete( entry.StorageEntry ) );
	}

	void OnInstalledRightClick( SpawnlistCollection.Entry entry )
	{
		var menu = MenuPanel.Open( this );
		menu.AddOption( "delete", "Remove", () => Collection.Uninstall( entry.WorkshopId ) );
	}
}

