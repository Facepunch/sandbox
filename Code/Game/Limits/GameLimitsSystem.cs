using Sandbox.UI;

/// <summary>
/// Host-only system that enforces per-player spawn limits for each <see cref="LimitCategory"/>.
/// Limits are configured via ConVars and tracked via the <see cref="Ownable"/> component
/// attached to every spawned object so counts are automatically decremented when objects are destroyed.
/// </summary>
public sealed class GameLimitsSystem : GameObjectSystem<GameLimitsSystem>
{
	/// <summary>
	/// When false, all limit checks pass without restriction.
	/// </summary>
	[Title( "Limits Enabled" ), Group( "Limits" )]
	[ConVar( "sb.limits.enabled", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	public static bool Enabled { get; set; } = true;

	/// <summary>
	/// Maximum props (including duplications) a single player may have active.
	/// </summary>
	[Title( "Max Props" ), Group( "Limits" )]
	[ConVar( "sb.limits.props", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 256 ), Step( 1 )]
	public static int MaxProps { get; set; } = 128;

	/// <summary>
	/// Maximum scripted entities a single player may have active.
	/// </summary>
	[Title( "Max Entities" ), Group( "Limits" )]
	[ConVar( "sb.limits.entities", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 128 ), Step( 1 )]
	public static int MaxEntities { get; set; } = 32;

	/// <summary>
	/// Maximum thrusters a single player may have active.
	/// </summary>
	[Title( "Max Thrusters" ), Group( "Limits" )]
	[ConVar( "sb.limits.thrusters", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 128 ), Step( 1 )]
	public static int MaxThrusters { get; set; } = 32;

	/// <summary>
	/// Maximum balloons a single player may have active.
	/// </summary>
	[Title( "Max Balloons" ), Group( "Limits" )]
	[ConVar( "sb.limits.balloons", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 128 ), Step( 1 )]
	public static int MaxBalloons { get; set; } = 8;

	/// <summary>
	/// Maximum wheels a single player may have active.
	/// </summary>
	[Title( "Max Wheels" ), Group( "Limits" )]
	[ConVar( "sb.limits.wheels", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 48 ), Step( 1 )]
	public static int MaxWheels { get; set; } = 16;

	/// <summary>
	/// Maximum constraints (ropes, welds, etc.) a single player may have active.
	/// </summary>
	[Title( "Max Constraints" ), Group( "Limits" )]
	[ConVar( "sb.limits.constraints", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 0, 2048 ), Step( 4 )]
	public static int MaxConstraints { get; set; } = 1024;

	/// <summary>
	/// Maps SteamId to per-category counts.
	/// </summary>
	class Count
	{
		private readonly Dictionary<string, int> _data = new();

		public int Get( string category ) => _data.GetValueOrDefault( category );
		public void Increment( string category ) => _data[category] = Get( category ) + 1;
		public void Decrement( string category ) => _data[category] = Math.Max( 0, Get( category ) - 1 );
		public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _data.GetEnumerator();
	}

	private readonly Dictionary<ulong, Count> _counts = new();

	public GameLimitsSystem( Scene scene ) : base( scene ) { }

	/// <summary>
	/// Returns the configured limit for the given category, or <c>-1</c> if unlimited.
	/// </summary>
	public static int GetLimit( string category ) => category switch
	{
		LimitCategory.Prop => MaxProps,
		LimitCategory.Entity => MaxEntities,
		LimitCategory.Thruster => MaxThrusters,
		LimitCategory.Balloon => MaxBalloons,
		LimitCategory.Wheel => MaxWheels,
		LimitCategory.Constraint => MaxConstraints,
		_ => -1,
	};

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
		GetOrCreateCounts( steamId ).Increment( category );
	}

	/// <summary>
	/// Decrements the player's count for the given category.
	/// Called automatically by <see cref="Ownable.OnDestroy"/>.
	/// </summary>
	public void Unregister( ulong steamId, string category )
	{
		if ( _counts.TryGetValue( steamId, out var counts ) )
			counts.Decrement( category );
	}

	/// <summary>
	/// Walks the spawned object trees, infers the limit category for each trackable
	/// object, checks all categories against current limits, and either registers
	/// everything or destroys the roots and notifies the owner.
	/// Returns <c>true</c> if the spawn was accepted.
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

		return true;
	}

	/// <summary>
	/// Infers the limit category for a single game object from its components.
	/// </summary>
	private static string InferCategory( GameObject go )
	{
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

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void SendLimitNotice( string text )
	{
		Notices.AddNotice( "🚫", Color.Red, text );
	}
}
