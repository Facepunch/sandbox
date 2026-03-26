using Sandbox;
using System.Text.Json.Serialization;

/// <summary>
/// Tracks which connection spawned this object, and optionally which spawn limit category it counts against.
/// </summary>
public sealed class Ownable : Component
{
	/// <summary>
	/// SteamId of the owning player. Synced from host so clients can read ownership.
	/// Persists after the player disconnects so limit tracking and cleanup still work.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public ulong OwnerSteamId { get; private set; }

	/// <summary>
	/// The owning connection, resolved live from <see cref="OwnerSteamId"/>.
	/// Returns <c>null</c> if the owner has disconnected.
	/// </summary>
	[Property, ReadOnly, JsonIgnore]
	public Connection Owner
	{
		get => Connection.All.FirstOrDefault( c => c.SteamId == OwnerSteamId );
		set => OwnerSteamId = value?.SteamId ?? 0;
	}

	/// <summary>
	/// Which limit category this object counts against, or <c>null</c>
	/// if this object is not tracked by the limits system.
	/// </summary>
	public string LimitCategory { get; private set; }

	public static Ownable Set( GameObject go, Connection owner )
	{
		var ownable = go.GetOrAddComponent<Ownable>();
		ownable.Owner = owner;
		return ownable;
	}

	/// <summary>
	/// Registers this object with the limits system for the given owner and category.
	/// Called by <see cref="GameLimitsSystem.Track"/>.
	/// </summary>
	internal void TrackLimit( ulong steamId, string category )
	{
		OwnerSteamId = steamId;
		LimitCategory = category;
	}

	protected override void OnDestroy()
	{
		if ( LimitCategory is not null )
			GameLimitsSystem.Current.Unregister( OwnerSteamId, LimitCategory );
	}
}
