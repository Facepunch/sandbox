
/// <summary>
/// Tracks how many <see cref="ISpawner"/> instances are currently loading, across every
/// spawn path (holding the spawner tool, spawn-menu clicks, dupes). Each spawner registers
/// its own <see cref="ISpawner.Loading"/> task in its constructor - see <see cref="Track"/>.
/// </summary>
public static class CloudLoadTracker
{
	static int _active;

	/// <summary>
	/// True while any spawner is still loading its payload.
	/// </summary>
	public static bool IsLoading => _active > 0;

	public static void Track( Task<bool> loading )
	{
		_active++;
		_ = Untrack( loading );
	}

	static async Task Untrack( Task<bool> loading )
	{
		try
		{
			await loading;
		}
		finally
		{
			_active--;
		}
	}
}
