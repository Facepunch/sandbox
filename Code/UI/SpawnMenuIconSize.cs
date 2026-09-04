namespace Sandbox;

/// <summary>
/// How big tiles are in spawn menu grids.
/// </summary>
public enum SpawnMenuIconSize
{
	Small,
	Medium,
	Large
}

public static class SpawnMenuIconSizeExtensions
{
	const string Cookie = "spawnmenu.icon_size";

	/// <summary>
	/// The user's chosen tile size, persisted between sessions.
	/// </summary>
	public static SpawnMenuIconSize Current
	{
		get => Game.Cookies.Get( Cookie, SpawnMenuIconSize.Medium );
		set => Game.Cookies.Set( Cookie, value );
	}

	/// <summary>
	/// Cell size to give a <see cref="Sandbox.UI.VirtualGrid"/> for this tile size.
	/// </summary>
	public static Vector2 ItemSize( this SpawnMenuIconSize size ) => size switch
	{
		SpawnMenuIconSize.Small => new Vector2( 90, 90 ),
		SpawnMenuIconSize.Large => new Vector2( 240, 180 ),
		_ => new Vector2( 160, 120 )
	};

	/// <summary>
	/// CSS class to put on the grid so tiles can adapt their text to the size.
	/// </summary>
	public static string CssClass( this SpawnMenuIconSize size ) => $"size-{size.ToString().ToLowerInvariant()}";

	/// <summary>
	/// Material icon representing this size in the toolbar and its menu.
	/// </summary>
	public static string Icon( this SpawnMenuIconSize size ) => size switch
	{
		SpawnMenuIconSize.Small => "view_comfy",
		SpawnMenuIconSize.Large => "crop_square",
		_ => "grid_view"
	};
}
