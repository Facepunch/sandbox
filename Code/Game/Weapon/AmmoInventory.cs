/// <summary>
/// Stores shared ammo pools on a player, keyed by <see cref="AmmoResource"/>.
/// Add this component to the player prefab alongside <see cref="PlayerInventory"/>.
/// </summary>
public sealed class AmmoInventory : Component
{
	/// <summary>
	/// Ammo pool: resource path → current count.
	/// Host-authoritative so server-side pickups replicate correctly to the owning client.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<string, int> Pool { get; set; } = new();

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
	/// Routes through the host when called from a client.
	/// </summary>
	public void SetAmmo( AmmoResource resource, int value )
	{
		if ( resource is null ) return;
		if ( !Networking.IsHost ) { SetAmmoRpc( resource, Math.Clamp( value, 0, resource.MaxReserve ) ); return; }
		Pool[resource.ResourcePath] = Math.Clamp( value, 0, resource.MaxReserve );
	}

	/// <summary>
	/// Adds ammo to the pool for the given resource (clamped to max).
	/// Returns the actual amount added (optimistic when called from a client).
	/// </summary>
	public int AddAmmo( AmmoResource resource, int count )
	{
		if ( resource is null ) return 0;
		if ( !Networking.IsHost ) { AddAmmoRpc( resource, count ); return count; }
		var current = GetAmmo( resource );
		var space = resource.MaxReserve - current;
		var toAdd = Math.Min( count, space );
		if ( toAdd <= 0 ) return 0;
		Pool[resource.ResourcePath] = current + toAdd;
		return toAdd;
	}

	/// <summary>
	/// Attempts to consume <paramref name="count"/> ammo from the pool.
	/// Returns <c>true</c> and deducts the ammo if successful (optimistic when called from a client).
	/// </summary>
	public bool TakeAmmo( AmmoResource resource, int count )
	{
		if ( resource is null ) return false;
		if ( !Networking.IsHost ) { TakeAmmoRpc( resource, count ); return GetAmmo( resource ) >= count; }
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

	[Rpc.Host]
	private void SetAmmoRpc( AmmoResource resource, int value )
	{
		Pool[resource.ResourcePath] = value;
	}

	[Rpc.Host]
	private void AddAmmoRpc( AmmoResource resource, int count )
	{
		var current = Pool.TryGetValue( resource.ResourcePath, out var c ) ? c : 0;
		var toAdd = Math.Min( count, resource.MaxReserve - current );
		if ( toAdd > 0 )
			Pool[resource.ResourcePath] = current + toAdd;
	}

	[Rpc.Host]
	private void TakeAmmoRpc( AmmoResource resource, int count )
	{
		var current = Pool.TryGetValue( resource.ResourcePath, out var c ) ? c : 0;
		if ( current >= count )
			Pool[resource.ResourcePath] = current - count;
	}
}
