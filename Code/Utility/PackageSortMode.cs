/// <summary>
/// A typed sort mode for finding packages.
/// </summary>
public enum PackageSortMode
{
	Popular,
	Newest,
	Trending,
	Random
}

public static class PackageSortModeExtensions
{
	/// <summary>
	/// Returns the API supported string equivalent of this sort mode.
	/// </summary>
	public static string ToIdentifier( this PackageSortMode sortMode )
	{
		return sortMode switch
		{
			PackageSortMode.Popular => "popular",
			PackageSortMode.Newest => "newest",
			PackageSortMode.Trending => "trending",
			PackageSortMode.Random => "random",
			_ => "popular"
		};
	}

	/// <summary>
	/// Material icon representing this sort mode in menus.
	/// </summary>
	public static string Icon( this PackageSortMode sortMode )
	{
		return sortMode switch
		{
			PackageSortMode.Newest => "new_releases",
			PackageSortMode.Trending => "trending_up",
			PackageSortMode.Random => "shuffle",
			_ => "whatshot"
		};
	}
}
