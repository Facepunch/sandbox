/// <summary>
/// Stores shared ammo pools on a player, keyed by <see cref="AmmoResource"/>.
/// Add this component to the player prefab alongside <see cref="PlayerInventory"/>.
/// </summary>
public sealed class AmmoInventory : Component
{
	/// <summary>
	/// Ammo pool: resource path → current count.
	/// </summary>
	[Sync] public Dictionary<string, int> Pool { get; set; } = new();

	/// <summary>
	/// Returns the current ammo count for the given resource.
	/// </summary>
	public int GetAmmo( AmmoResource resource )
	{
		if ( resource is null ) return 0;
		return Pool.TryGetValue( resource.ResourcePath, out var count ) ? count : 0;
	}

	/// <summary>
	/// Sets the ammo count for the given resource directly, clamped to [0, max].
	/// </summary>
	public void SetAmmo( AmmoResource resource, int value )
	{
		if ( resource is null ) return;
		Pool[resource.ResourcePath] = Math.Clamp( value, 0, resource.MaxReserve );
	}

	/// <summary>
	/// Adds ammo to the pool for the given resource (clamped to max).
	/// Returns the actual amount added.
	/// </summary>
	public int AddAmmo( AmmoResource resource, int count )
	{
		if ( resource is null ) return 0;
		var current = GetAmmo( resource );
		var space = resource.MaxReserve - current;
		var toAdd = Math.Min( count, space );
		if ( toAdd <= 0 ) return 0;
		Pool[resource.ResourcePath] = current + toAdd;
		return toAdd;
	}

	/// <summary>
	/// Attempts to consume <paramref name="count"/> ammo from the pool.
	/// Returns <c>true</c> and deducts the ammo if successful.
	/// </summary>
	public bool TakeAmmo( AmmoResource resource, int count )
	{
		if ( resource is null ) return false;
		var current = GetAmmo( resource );
		if ( current < count ) return false;
		Pool[resource.ResourcePath] = current - count;
		return true;
	}

	/// <summary>
	/// Returns true if there is at least <paramref name="count"/> ammo in the pool.
	/// </summary>
	public bool HasAmmo( AmmoResource resource, int count = 1 )
	{
		return GetAmmo( resource ) >= count;
	}
}
