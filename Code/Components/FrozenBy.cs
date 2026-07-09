/// <summary>
/// Tracks which connection last froze this object with the physgun.
/// Added when frozen, removed when unfrozen.
/// </summary>
public sealed class FrozenBy : Component
{
	[Sync( SyncFlags.FromHost )]
	public Guid FreezerId { get; set; }

	/// <summary>
	/// Marks a gameObject as frozen by a connection, adding the component if it doesn't have one yet.
	/// </summary>
	public static void Set( GameObject go, Guid freezerId )
	{
		var frozen = go.GetOrAddComponent<FrozenBy>();
		frozen.FreezerId = freezerId;
	}

	/// <summary>
	/// Clears the frozen marker from a gameObject if it has one.
	/// </summary>
	public static void Clear( GameObject go )
	{
		if ( go.Components.TryGet<FrozenBy>( out var frozen ) )
			frozen.Destroy();
	}
}
