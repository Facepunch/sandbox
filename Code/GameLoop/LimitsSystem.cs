using Sandbox.UI;

/// <summary>
/// Enforces configurable limits on spawning and tool usage.
/// Maintains a per-player tracked object list populated from post-events.
/// Limit checks iterate only the player's objects, not the entire scene.
/// </summary>
public sealed class LimitsSystem : GameObjectSystem<LimitsSystem>, ISpawnEvents, IToolActionEvents
{
	[Range( -1, 1024 )]
	[Title( "Max Props Per Player" ), Group( "Limits" )]
	[ConVar( "sb.limit.props", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum props per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxPropsPerPlayer { get; set; } = -1;

	[Range( -1, 16 )]
	[Title( "Max Explosives Per Player" ), Group( "Limits" )]
	[ConVar( "sb.limit.explosives", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum explosive props per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxExplosivesPerPlayer { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Balloons Per Player" ), Group( "Limits" )]
	[ConVar( "sb.limit.balloons", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum balloons per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxBalloons { get; set; } = -1;

	[Range( -1, 512 )]
	[Title( "Max Constraints Per Player" ), Group( "Limits" )]
	[ConVar( "sb.limit.constraints", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum constraints per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxConstraints { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Emitters Per Player" ), Group( "Limits" )]
	[ConVar( "sb.limit.emitters", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum emitters per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxEmitters { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Thrusters Per Player" ), Group( "Limits" )]
	[ConVar( "sb.limit.thrusters", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum thrusters per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxThrusters { get; set; } = -1;

	[Range( -1, 32 )]
	[Title( "Max Hoverballs Per Player" ), Group( "Limits" )]
	[ConVar( "sb.limit.hoverballs", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum hoverballs per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxHoverballs { get; set; } = -1;

	[Range( -1, 32 )]
	[Title( "Max Wheels Per Player" ), Group( "Limits" )]
	[ConVar( "sb.limit.wheels", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum wheels per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxWheels { get; set; } = -1;

	/// <summary>
	/// Per-player tracked objects keyed by SteamId.
	/// </summary>
	private readonly Dictionary<long, List<GameObject>> _tracked = new();

	/// <summary>
	/// Fast lookup to check if a GameObject is already tracked by any player.
	/// </summary>
	private readonly HashSet<GameObject> _allTracked = new();

	public LimitsSystem( Scene scene ) : base( scene )
	{
	}

	/// <summary>
	/// Returns true if the given count meets or exceeds the limit.
	/// A limit of -1 means unlimited (never exceeded). A limit of 0 means none allowed (always exceeded).
	/// </summary>
	private static bool IsExceeded( int limit, int count ) => limit >= 0 && count >= limit;

	private List<GameObject> GetOrCreateList( long steamId )
	{
		if ( !_tracked.TryGetValue( steamId, out var list ) )
		{
			list = new List<GameObject>();
			_tracked[steamId] = list;
		}

		return list;
	}

	private void Track( long steamId, GameObject go )
	{
		if ( !go.IsValid() ) return;
		if ( !_allTracked.Add( go ) ) return;

		GetOrCreateList( steamId ).Add( go );
	}

	private void Track( long steamId, List<GameObject> objects )
	{
		foreach ( var go in objects )
			Track( steamId, go );
	}

	/// <summary>
	/// Count tracked objects for a player, pruning destroyed ones. Applies an optional filter.
	/// </summary>
	private int Count( long steamId, Func<GameObject, bool> filter = null )
	{
		if ( !_tracked.TryGetValue( steamId, out var list ) )
			return 0;

		var count = 0;
		for ( int i = list.Count - 1; i >= 0; i-- )
		{
			var go = list[i];
			if ( !go.IsValid() )
			{
				_allTracked.Remove( go );
				list.RemoveAt( i );
				continue;
			}

			if ( filter is null || filter( go ) )
				count++;
		}

		return count;
	}

	void ISpawnEvents.OnSpawn( ISpawnEvents.SpawnData e )
	{
		if ( e.Player is null ) return;

		var steamId = e.Player.SteamId;

		// Duplicator: batch pre-check — reject entire dupe if it would exceed limits
		if ( e.Spawner is DuplicatorSpawner dupeSpawner )
		{
			var dupeObjectCount = dupeSpawner.Dupe?.Objects?.Count ?? 0;

			if ( MaxPropsPerPlayer >= 0 && dupeObjectCount > 0 )
			{
				var current = Count( steamId, go => go.GetComponent<Prop>().IsValid() );
				if ( current + dupeObjectCount > MaxPropsPerPlayer )
				{
					e.Cancelled = true;
					NotifyLimit( e.Player, "props", MaxPropsPerPlayer );
					return;
				}
			}

			if ( MaxExplosivesPerPlayer >= 0 )
			{
				var explosivesInDupe = CountExplosivesInDupe( dupeSpawner );
				if ( explosivesInDupe > 0 )
				{
					var current = Count( steamId, go =>
					{
						var prop = go.GetComponent<Prop>();
						return prop.IsValid() && prop.Model?.Data?.Explosive == true;
					} );

					if ( current + explosivesInDupe > MaxExplosivesPerPlayer )
					{
						e.Cancelled = true;
						NotifyLimit( e.Player, "explosives", MaxExplosivesPerPlayer );
						return;
					}
				}
			}

			return;
		}

		if ( MaxPropsPerPlayer >= 0 && e.Spawner is PropSpawner )
		{
			var count = Count( steamId, go => go.GetComponent<Prop>().IsValid() );
			if ( IsExceeded( MaxPropsPerPlayer, count ) )
			{
				e.Cancelled = true;
				NotifyLimit( e.Player, "props", MaxPropsPerPlayer );
				return;
			}
		}

		if ( MaxExplosivesPerPlayer >= 0 && IsExplosiveSpawn( e.Spawner ) )
		{
			var count = Count( steamId, go =>
			{
				var prop = go.GetComponent<Prop>();
				return prop.IsValid() && prop.Model?.Data?.Explosive == true;
			} );

			if ( IsExceeded( MaxExplosivesPerPlayer, count ) )
			{
				e.Cancelled = true;
				NotifyLimit( e.Player, "explosives", MaxExplosivesPerPlayer );
				return;
			}
		}
	}

	void ISpawnEvents.OnPostSpawn( ISpawnEvents.PostSpawnData e )
	{
		if ( e.Player is null || e.Objects is null ) return;

		Track( e.Player.SteamId, e.Objects );
	}

	void IToolActionEvents.OnToolAction( IToolActionEvents.ActionData e )
	{
		if ( e.Input == ToolInput.Reload ) return;
		if ( e.Player is null ) return;

		// TODO: this could be better, register something with the tool instead?
		if ( CheckToolLimit<Balloon, BalloonEntity>( e, MaxBalloons ) ) return;
		if ( CheckToolLimit<ThrusterTool, ThrusterEntity>( e, MaxThrusters ) ) return;
		if ( CheckToolLimit<EmitterTool, EmitterEntity>( e, MaxEmitters, ToolInput.Primary ) ) return;
		if ( CheckToolLimit<HoverballTool, HoverballEntity>( e, MaxHoverballs, ToolInput.Primary ) ) return;
		if ( CheckToolLimit<WheelTool, WheelEntity>( e, MaxWheels, ToolInput.Primary ) ) return;

		// TODO: same here :S
		if ( MaxConstraints >= 0 && ( e.Tool is BaseConstraintToolMode || e.Tool is KeepUpright ) )
		{
			var count = Count( e.Player.SteamId, go => go.Tags.Contains( "constraint" ) );
			if ( IsExceeded( MaxConstraints, count ) )
			{
				e.Cancelled = true;
				NotifyLimit( e.Player, GetToolName( e.Tool ), MaxConstraints );
			}
		}
	}

	void IToolActionEvents.OnPostToolAction( IToolActionEvents.PostActionData e )
	{
		if ( !e.Player.IsValid() || e.CreatedObjects is not { Count: > 0 } ) return;

		Track( e.Player.SteamId, e.CreatedObjects );
	}

	/// <summary>
	/// Check a per-player tool limit. Returns true if the tool type matched.
	/// </summary>
	private bool CheckToolLimit<TTool, TEntity>( IToolActionEvents.ActionData e, int limit, ToolInput? creationInput = null )
		where TTool : ToolMode
		where TEntity : Component
	{
		if ( e.Tool is not TTool ) return false;
		if ( limit < 0 ) return true;
		if ( creationInput.HasValue && e.Input != creationInput.Value ) return true;

		var count = Count( e.Player.SteamId, go => go.GetComponent<TEntity>().IsValid() );
		if ( IsExceeded( limit, count ) )
		{
			e.Cancelled = true;
			NotifyLimit( e.Player, GetToolName( e.Tool ), limit );
		}

		return true;
	}

	private static bool IsExplosiveSpawn( ISpawner spawner )
	{
		if ( spawner is PropSpawner propSpawner )
			return propSpawner.Model?.Data?.Explosive == true;

		if ( spawner is DuplicatorSpawner dupeSpawner )
			return dupeSpawner.Dupe?.PreviewModels?.Any( m => m.Model?.Data?.Explosive == true ) == true;

		return false;
	}

	private static int CountExplosivesInDupe( DuplicatorSpawner spawner )
	{
		if ( spawner.Dupe?.PreviewModels is null ) return 0;
		return spawner.Dupe.PreviewModels.Count( m => m.Model?.Data?.Explosive == true );
	}

	private static string GetToolName( ToolMode tool ) => tool?.TypeDescription?.Title ?? tool?.GetType().Name ?? "Unknown";

	private static void NotifyLimit( PlayerData player, string category, int limit )
	{
		var target = player?.Connection;
		if ( target is null ) return;

		Notices.SendNotice( target, "block", Color.Red, $"Limit reached: {category} ({limit})", 3 );
	}
}
