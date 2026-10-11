using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Window for creating a new spawnlist
/// </summary>
public sealed class SpawnlistCreateForm
{
	[Property, HideLabel, Placeholder( "#spawnmenu.spawnlist.name_placeholder" ), Order( 0 )]
	public string Name { get; set; } = "";

	/// <summary>
	/// Create needs a name.
	/// </summary>
	public bool HasName => !string.IsNullOrWhiteSpace( Name );

	SpawnlistItem _initialItem;
	Action _onCreated;
	Action<Storage.Entry> _onEntryCreated;
	UIModal _modal;

	[Separator, Button( "#spawnmenu.spawnlist.create_button", "add" ), EnabledIf( nameof( HasName ) ), Order( 1 )]
	public void Create()
	{
		if ( !HasName ) return;

		var name = Name.Trim();

		SpawnlistData.Create( name, out var entry );
		if ( _initialItem is not null )
			SpawnlistData.AddItem( entry, _initialItem );

		Notices.AddNotice( "list_alt", Color.Cyan, Game.Language.GetPhrase( "spawnmenu.spawnlist.created_notice", new() { { "name", name } } ) );

		_onCreated?.Invoke();
		_onEntryCreated?.Invoke( entry );

		_modal?.Close();
	}

	[SameLine, Button( "#spawnmenu.common.cancel" ), Order( 2 )]
	public void Cancel() => _modal?.Close();

	/// <summary>
	/// Opens the window over the menu the panel is in
	/// </summary>
	/// <param name="initialItem">An item the new list starts with</param>
	/// <param name="name">What the name box starts with</param>
	public static void Show( Panel source, Action onCreated = null, Action<Storage.Entry> onEntryCreated = null, SpawnlistItem initialItem = null, string name = null )
	{
		var form = new SpawnlistCreateForm
		{
			Name = name ?? "",
			_initialItem = initialItem,
			_onCreated = onCreated,
			_onEntryCreated = onEntryCreated
		};

		var modal = UIModal.Show( source, "#spawnmenu.spawnlist.create_title", form );
		if ( modal is null ) return;

		form._modal = modal;
		modal.OnSubmit = form.Create;
	}
}
