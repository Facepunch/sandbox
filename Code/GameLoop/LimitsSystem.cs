using Sandbox.Npcs;
using Sandbox.UI;

/// <summary>
/// Enforces configurable limits on spawning and tool usage.
/// Maintains a per-player tracked object list populated from post-events.
/// Limit checks iterate only the player's objects, not the entire scene.
/// </summary>
internal sealed class LimitsSystem : GameObjectSystem<LimitsSystem>, Global.ISpawnEvents, IToolActionEvents
{
	[Range( -1, 1024 )]
	[Title( "Max Props" ), Group( "Limits" ), Order( 0 )]
	[ConVar( "sandbox.limit.props", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum props per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxPropsPerPlayer { get; set; } = -1;

	[Range( -1, 16 )]
	[Title( "Max Explosives" ), Group( "Limits" ), Order( 1 )]
	[ConVar( "sandbox.limit.explosives", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum explosive props per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxExplosivesPerPlayer { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Balloons" ), Group( "Limits" ), Order( 3 )]
	[ConVar( "sandbox.limit.balloons", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum balloons per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxBalloons { get; set; } = -1;

	[Range( -1, 512 )]
	[Title( "Max Constraints" ), Group( "Limits" ), Order( 2 )]
	[ConVar( "sandbox.limit.constraints", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum constraints per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxConstraints { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Emitters" ), Group( "Limits" ), Order( 4 )]
	[ConVar( "sandbox.limit.emitters", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum emitters per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxEmitters { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Thrusters" ), Group( "Limits" ), Order( 5 )]
	[ConVar( "sandbox.limit.thrusters", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum thrusters per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxThrusters { get; set; } = -1;

	[Range( -1, 32 )]
	[Title( "Max Hoverballs" ), Group( "Limits" ), Order( 6 )]
	[ConVar( "sandbox.limit.hoverballs", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum hoverballs per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxHoverballs { get; set; } = -1;

	[Range( -1, 32 )]
	[Title( "Max Wheels" ), Group( "Limits" ), Order( 7 )]
	[ConVar( "sandbox.limit.wheels", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum wheels per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxWheels { get; set; } = -1;

	[Range( -1, 256 )]
	[Title( "Max Texts" ), Group( "Limits" ), Order( 8 )]
	[ConVar( "sandbox.limit.texts", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum placed texts per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxTexts { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Speakers" ), Group( "Limits" ), Order( 9 )]
	[ConVar( "sandbox.limit.speakers", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum speakers per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxSpeakers { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Cameras" ), Group( "Limits" ), Order( 10 )]
	[ConVar( "sandbox.limit.cameras", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum cameras per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxCameras { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Dynamite" ), Group( "Limits" ), Order( 11 )]
	[ConVar( "sandbox.limit.dynamite", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum dynamite per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxDynamite { get; set; } = -1;

	[Range( -1, 128 )]
	[Title( "Max Lights" ), Group( "Limits" ), Order( 12 )]
	[ConVar( "sandbox.limit.lights", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum lights (bulbs and spot lights) per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxLights { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max NPCs" ), Group( "Limits" ), Order( 13 )]
	[ConVar( "sandbox.limit.npcs", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum NPCs per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxNpcs { get; set; } = -1;

	[Range( -1, 32 )]
	[Title( "Max Vehicles" ), Group( "Limits" ), Order( 14 )]
	[ConVar( "sandbox.limit.vehicles", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum vehicles per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxVehicles { get; set; } = -1;

	[Range( -1, 64 )]
	[Title( "Max Ragdolls" ), Group( "Limits" ), Order( 15 )]
	[ConVar( "sandbox.limit.ragdolls", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Maximum ragdolls per player. -1 = unlimited, 0 = none allowed." )]
	public static int MaxRagdolls { get; set; } = -1;

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

	void Global.ISpawnEvents.OnSpawn( Global.ISpawnEvents.SpawnData e )
	{
		if ( e.Player is null ) return;

		var steamId = (long)e.Player.SteamId;

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
					NotifyLimit( e.Player, "#limit.props", MaxPropsPerPlayer );
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
						NotifyLimit( e.Player, "#limit.explosives", MaxExplosivesPerPlayer );
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
				NotifyLimit( e.Player, "#limit.props", MaxPropsPerPlayer );
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
				NotifyLimit( e.Player, "#limit.explosives", MaxExplosivesPerPlayer );
				return;
			}
		}

		// Spawn menu entities, by what their prefab is
		if ( e.Spawner?.Prefab is { } prefab )
		{
			if ( CheckSpawnLimit( e, steamId, MaxDynamite, "#limit.dynamite", prefab, IsDynamite ) ) return;
			if ( CheckSpawnLimit( e, steamId, MaxLights, "#limit.lights", prefab, IsLight ) ) return;
			if ( CheckSpawnLimit( e, steamId, MaxNpcs, "#limit.npcs", prefab, IsNpc ) ) return;
			if ( CheckSpawnLimit( e, steamId, MaxVehicles, "#limit.vehicles", prefab, IsVehicle ) ) return;
		}

		// Props whose model is a ragdoll, from the spawn menu or a mounted game
		if ( MaxRagdolls >= 0 && IsRagdollModel( SpawnedModel( e.Spawner ) ) )
		{
			if ( IsExceeded( MaxRagdolls, Count( steamId, IsRagdoll ) ) )
			{
				e.Cancelled = true;
				NotifyLimit( e.Player, "#limit.ragdolls", MaxRagdolls );
			}
		}
	}

	static bool IsDynamite( GameObject go ) => go.GetComponentInChildren<DynamiteEntity>( true ).IsValid();
	static bool IsLight( GameObject go ) => go.GetComponentInChildren<PointLightEntity>( true ).IsValid() || go.GetComponentInChildren<SpotLightEntity>( true ).IsValid();
	static bool IsNpc( GameObject go ) => go.GetComponentInChildren<Npc>( true ).IsValid();
	static bool IsVehicle( GameObject go ) => go.GetComponentInChildren<FourWheelVehicle>( true ).IsValid();
	static bool IsRagdollModel( Model model ) => (model?.Physics?.Parts?.Count ?? 0) > 1;
	static bool IsRagdoll( GameObject go ) => IsRagdollModel( go.GetComponent<Prop>()?.Model );

	static Model SpawnedModel( ISpawner spawner ) => spawner switch
	{
		PropSpawner prop => prop.Model,
		MountSpawner mount => mount.Model,
		_ => null
	};

	/// <summary>
	/// Check a per-player limit on spawn menu entities. Returns true if the spawn was one of these and got cancelled.
	/// </summary>
	private bool CheckSpawnLimit( Global.ISpawnEvents.SpawnData e, long steamId, int limit, string category, GameObject prefab, Func<GameObject, bool> isKind )
	{
		if ( limit < 0 || !isKind( prefab ) ) return false;
		if ( !IsExceeded( limit, Count( steamId, isKind ) ) ) return false;

		e.Cancelled = true;
		NotifyLimit( e.Player, category, limit );
		return true;
	}

	void Global.ISpawnEvents.OnPostSpawn( Global.ISpawnEvents.PostSpawnData e )
	{
		if ( e.Player is null || e.Objects is null ) return;

		Track( (long)e.Player.SteamId, e.Objects );
	}

	void IToolActionEvents.OnToolAction( IToolActionEvents.ActionData e )
	{
		if ( e.Input == ToolInput.Reload ) return;
		if ( e.Player is null ) return;

		// TODO: this could be better, register something with the tool instead?
		if ( CheckToolLimit<BalloonTool, BalloonEntity>( e, MaxBalloons ) ) return;
		if ( CheckToolLimit<ThrusterTool, ThrusterEntity>( e, MaxThrusters ) ) return;
		if ( CheckToolLimit<EmitterTool, EmitterEntity>( e, MaxEmitters ) ) return;
		if ( CheckToolLimit<HoverballTool, HoverballEntity>( e, MaxHoverballs ) ) return;
		if ( CheckToolLimit<WheelTool, WheelEntity>( e, MaxWheels ) ) return;
		if ( CheckToolLimit<TextTool, TextEntity>( e, MaxTexts ) ) return;
		if ( CheckToolLimit<SpeakerTool, SpeakerEntity>( e, MaxSpeakers ) ) return;
		if ( CheckToolLimit<CameraTool, CameraEntity>( e, MaxCameras ) ) return;

		// the light tool also updates the bulb you aim at, that isn't another light
		if ( e.Tool is LightTool lightTool )
		{
			if ( MaxLights >= 0 && lightTool.PlacingNew && IsExceeded( MaxLights, Count( (long)e.Player.SteamId, IsLight ) ) )
			{
				e.Cancelled = true;
				NotifyLimit( e.Player, GetToolName( e.Tool ), MaxLights );
			}

			return;
		}

		// TODO: same here :S
		if ( MaxConstraints >= 0 && (e.Tool is BaseConstraintToolMode || e.Tool is KeepUprightTool) )
		{
			var count = Count( (long)e.Player.SteamId, go => go.Tags.Contains( "constraint" ) );
			if ( IsExceeded( MaxConstraints, count ) )
			{
				e.Cancelled = true;
				NotifyLimit( e.Player, GetToolName( e.Tool ), MaxConstraints );
			}
		}
	}

	void IToolActionEvents.OnPostToolAction( IToolActionEvents.PostActionData e )
	{
		if ( e.Player is null || e.CreatedObjects is not { Count: > 0 } ) return;

		Track( (long)e.Player.SteamId, e.CreatedObjects );
	}

	/// <summary>
	/// Check a per-player tool limit. Returns true if the tool type matched. Every action these tools send to the host places one.
	/// </summary>
	private bool CheckToolLimit<TTool, TEntity>( IToolActionEvents.ActionData e, int limit )
		where TTool : ToolMode
		where TEntity : Component
	{
		if ( e.Tool is not TTool ) return false;
		if ( limit < 0 ) return true;

		var count = Count( (long)e.Player.SteamId, go => go.GetComponent<TEntity>().IsValid() );
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

	private static string GetToolName( ToolMode tool ) => tool?.TypeDescription?.Title ?? tool?.GetType().Name ?? "#limit.unknown";

	private static void NotifyLimit( Connection player, string category, int limit )
	{
		if ( player is null ) return;

		Notices.SendLocalizedNotice( player, "block", Color.Red, "#limit.reached", new()
		{
			{ "category", category },
			{ "limit", limit.ToString() }
		}, 3 );
	}
}
