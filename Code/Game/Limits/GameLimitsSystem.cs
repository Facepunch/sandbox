using Sandbox.UI;

/// <summary>
/// Host-only system that enforces per-player spawn limits for each <see cref="LimitCategory"/>.
/// Limits are configured via ConVars and tracked via the <see cref="Ownable"/> component
/// attached to every spawned object so counts are automatically decremented when objects are destroyed.
/// </summary>
public sealed partial class GameLimitsSystem : GameObjectSystem<GameLimitsSystem>
{
	private readonly Dictionary<ulong, Count> _counts = new();

	/// <summary>
	/// The local player's per-category counts, populated by the host via RPC.
	/// </summary>
	private static readonly Dictionary<string, int> _localCounts = new();

	/// <summary>
	/// Returns the local player's current active count for the given category.
	/// </summary>
	public static int GetLocalCount( string category ) =>
		_localCounts.GetValueOrDefault( category, 0 );

	public GameLimitsSystem( Scene scene ) : base( scene ) { }

	/// <summary>
	/// Returns the current active count for the given player and category.
	/// </summary>
	public int GetCount( ulong steamId, string category )
	{
		if ( !_counts.TryGetValue( steamId, out var counts ) )
			return 0;

		return counts.Get( category );
	}

	/// <summary>
	/// Returns <c>true</c> if the player can spawn <paramref name="count"/> more objects
	/// of the given category without exceeding the limit.
	/// </summary>
	public bool CanSpawn( ulong steamId, string category, int count = 1 )
	{
		var limit = GetLimit( category );
		if ( limit < 0 ) return true;

		return GetCount( steamId, category ) + count <= limit;
	}

	/// <summary>
	/// Returns <c>true</c> if the owner has reached their limit for <paramref name="category"/>
	/// and the spawn should be rejected. Sends a rejection notice to the owner as a side effect.
	/// Always returns <c>false</c> when not the host or when limits are disabled.
	/// </summary>
	public bool IsOverLimit( Connection owner, string category, int count = 1, bool notify = true )
	{
		if ( !Networking.IsHost ) return false;
		if ( !Enabled ) return false;
		if ( owner is null ) return false;

		if ( CanSpawn( owner.SteamId, category, count ) )
			return false;

		var limit = GetLimit( category );
		var current = GetCount( owner.SteamId, category );

		if ( notify )
		{
			using ( Rpc.FilterInclude( owner ) )
			{
				SendLimitNotice( $"{category.ToTitleCase()} limit reached ({current}/{limit})" );
			}
		}

		return true;
	}

	/// <summary>
	/// Configures the <see cref="Ownable"/> on <paramref name="go"/> to track against the player's
	/// limit for the given category and increments their count. Safe to call only on the host.
	/// </summary>
	public void Track( Connection owner, string category, GameObject go )
	{
		if ( owner is null || !go.IsValid() ) return;

		var steamId = owner.SteamId;
		var ownable = go.GetOrAddComponent<Ownable>();
		ownable.TrackLimit( steamId, category );
		var counts = GetOrCreateCounts( steamId );
		counts.Increment( category );
		PushCountToPlayer( owner, category, counts.Get( category ) );
	}

	/// <summary>
	/// Decrements the player's count for the given category.
	/// Called automatically by <see cref="Ownable.OnDestroy"/>.
	/// </summary>
	public void Unregister( ulong steamId, string category )
	{
		if ( !_counts.TryGetValue( steamId, out var counts ) ) return;

		counts.Decrement( category );
		var conn = Connection.All.FirstOrDefault( c => c.SteamId == steamId );
		if ( conn is not null )
			PushCountToPlayer( conn, category, counts.Get( category ) );
	}

	/// <summary>
	/// Walks the locally-built (unnetworked) object trees, infers the limit category for each
	/// trackable object, and checks all categories against current limits.
	/// On success: registers tracking and returns <c>true</c>. The caller must then call
	/// <c>NetworkSpawn</c> on each root.
	/// On failure: destroys roots locally (no network traffic) and notifies the owner,
	/// then returns <c>false</c>.
	/// </summary>
	public bool TrackSpawned( Connection owner, List<GameObject> roots )
	{
		if ( !Networking.IsHost || !Enabled || owner is null ) return true;

		var toTrack = new List<(GameObject go, string category)>();
		foreach ( var root in roots )
			CollectTrackable( root, toTrack );

		// Tally per-category delta
		var delta = new Count();
		foreach ( var (_, cat) in toTrack )
			delta.Increment( cat );

		// Check every touched category
		var steamId = owner.SteamId;
		foreach ( var (cat, count) in delta )
		{
			if ( CanSpawn( steamId, cat, count ) ) continue;

			var limit = GetLimit( cat );
			var current = GetCount( steamId, cat );

			using ( Rpc.FilterInclude( owner ) )
				SendLimitNotice( $"{cat.ToTitleCase()} limit reached ({current}/{limit})" );

			foreach ( var root in roots )
				if ( root.IsValid() ) root.Destroy();
			roots.Clear();

			return false;
		}

		// All OK — register
		var counts = GetOrCreateCounts( steamId );
		foreach ( var (go, cat) in toTrack )
		{
			var ownable = go.GetOrAddComponent<Ownable>();
			ownable.TrackLimit( steamId, cat );
			counts.Increment( cat );
		}

		// Push updated counts to owner for each modified category
		foreach ( var (cat, _) in delta )
			PushCountToPlayer( owner, cat, counts.Get( cat ) );

		return true;
	}

	/// <summary>
	/// Infers the limit category for a single game object from its components.
	/// </summary>
	private static string InferCategory( GameObject go )
	{
		var hint = go.GetComponent<Ownable>( true )?.HintCategory;
		if ( hint is not null ) return hint;

		if ( go.GetComponent<ConstraintCleanup>() is not null ) return LimitCategory.Constraint;
		if ( go.GetComponent<ThrusterEntity>() is not null ) return LimitCategory.Thruster;
		if ( go.GetComponent<WheelEntity>() is not null ) return LimitCategory.Wheel;
		if ( go.GetComponent<Prop>() is not null ) return LimitCategory.Prop;
		return null;
	}

	private static void CollectTrackable( GameObject go, List<(GameObject, string)> result )
	{
		var cat = InferCategory( go );
		if ( cat is not null )
			result.Add( (go, cat) );

		foreach ( var child in go.Children )
			CollectTrackable( child, result );
	}

	private Count GetOrCreateCounts( ulong steamId )
	{
		if ( !_counts.TryGetValue( steamId, out var counts ) )
		{
			counts = new Count();
			_counts[steamId] = counts;
		}

		return counts;
	}

	private static void PushCountToPlayer( Connection owner, string category, int count )
	{
		using ( Rpc.FilterInclude( owner ) )
			ReceiveCountUpdate( category, count );
	}

	/// <summary>
	/// Received by a client (or the host acting as client) to update their local count store.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void ReceiveCountUpdate( string category, int count )
	{
		_localCounts[category] = count;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void SendLimitNotice( string text )
	{
		Notices.AddNotice( "🚫", Color.Red, text );
	}
}
