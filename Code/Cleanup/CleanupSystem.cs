namespace Sandbox;

public interface ICleanupEvents
{
	public void OnCleanup( int removedObjects, int restoredObjects );
}

/// <summary>
/// A system that tracks the baseline scene state and allows resetting the map to its original state.
/// Removes all spawned props and restores destroyed map objects while leaving players untouched.
/// </summary>
public sealed class CleanupSystem : GameObjectSystem<CleanupSystem>, ISceneLoadingEvents
{
	/// <summary>
	/// Set of GameObjects that existed in the original scene baseline.
	/// </summary>
	private readonly HashSet<Guid> _baselineObjectIds = new();

	/// <summary>
	/// Serialized data of baseline objects so we can restore them if destroyed.
	/// </summary>
	private readonly Dictionary<Guid, string> _baselineObjectData = new();

	/// <summary>
	/// The resource path of the loaded scene, used to reload the baseline.
	/// </summary>
	private string _loadedScenePath;

	/// <summary>
	/// Whether a baseline has been captured.
	/// </summary>
	public bool HasBaseline => _baselineObjectIds.Count > 0;

	public CleanupSystem( Scene scene ) : base( scene )
	{
	}

	void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options )
	{
		// Clear any existing baseline when a new scene is loading
		_baselineObjectIds.Clear();
		_baselineObjectData.Clear();
		_loadedScenePath = null;
	}

	async Task ISceneLoadingEvents.OnLoad( Scene scene, SceneLoadOptions options, LoadingContext context )
	{
		// Wait for next frame to ensure all objects are spawned
		await Task.Yield();

		// Could be null if the scene was unloaded before this runs
		if ( !Scene.IsValid() ) return;

		CaptureBaseline();

		var sceneFile = options.GetSceneFile();
		if ( sceneFile is not null && !string.IsNullOrEmpty( sceneFile.ResourcePath ) )
		{
			_loadedScenePath = sceneFile.ResourcePath;
		}
	}

	/// <summary>
	/// Captures the current scene state as the baseline.
	/// All objects that exist at this point are considered part of the original map.
	/// </summary>
	public void CaptureBaseline()
	{
		_baselineObjectIds.Clear();
		_baselineObjectData.Clear();

		foreach ( var go in Scene.Children?.ToArray() ?? [] )
		{
			CaptureObjectRecursive( go );
		}

		Log.Info( $"CleanupSystem: Captured baseline with {_baselineObjectIds.Count} objects." );
	}

	private void CaptureObjectRecursive( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		// Skip player objects
		if ( IsPlayerObject( go ) )
			return;

		if ( go.Flags.Contains( GameObjectFlags.DontDestroyOnLoad ) )
			return;

		_baselineObjectIds.Add( go.Id );

		var serialized = go.Serialize();
		if ( serialized is not null )
		{
			_baselineObjectData[go.Id] = serialized.ToJsonString();
		}

		foreach ( var child in go.Children?.ToArray() ?? [] )
		{
			CaptureObjectRecursive( child );
		}
	}

	/// <summary>
	/// Determines if a GameObject is a player or belongs to a player.
	/// </summary>
	private static bool IsPlayerObject( GameObject go )
	{
		if ( !go.IsValid() )
			return false;

		if ( go.Components.Get<Player>( true ) is not null )
			return true;

		if ( go.Components.Get<PlayerData>( true ) is not null )
			return true;

		var parent = go.Parent;
		while ( parent is not null && parent != go.Scene )
		{
			if ( parent.Components.Get<Player>( true ) is not null )
				return true;
			if ( parent.Components.Get<PlayerData>( true ) is not null )
				return true;
			parent = parent.Parent;
		}

		return false;
	}

	/// <summary>
	/// Cleans up the scene by removing all spawned objects and restoring destroyed baseline objects.
	/// Players and their belongings are preserved.
	/// </summary>
	public void Cleanup()
	{
		if ( !HasBaseline )
		{
			Log.Warning( "CleanupSystem: No baseline captured. Cannot cleanup." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "CleanupSystem: Only the host can perform cleanup." );
			return;
		}

		var removedCount = 0;
		var restoredCount = 0;
		var objectsToRemove = new List<GameObject>();
		var existingBaselineIds = new HashSet<Guid>();

		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( !go.IsValid() )
				continue;

			// Never remove player objects
			if ( IsPlayerObject( go ) )
				continue;

			if ( go.Flags.Contains( GameObjectFlags.DontDestroyOnLoad ) )
				continue;

			if ( _baselineObjectIds.Contains( go.Id ) )
			{
				existingBaselineIds.Add( go.Id );
			}
			else
			{
				if ( go.Parent == Scene )
				{
					objectsToRemove.Add( go );
				}
			}
		}

		// Remove spawned objects
		foreach ( var go in objectsToRemove )
		{
			if ( go.IsValid() )
			{
				go.Destroy();
				removedCount++;
			}
		}

		// Restore destroyed baseline objects
		foreach ( var kvp in _baselineObjectData )
		{
			var id = kvp.Key;

			// Skip if the object still exists
			if ( existingBaselineIds.Contains( id ) )
				continue;

			// Skip if we already processed the parent object
			var go = Scene.Directory.FindByGuid( id );
			if ( go.IsValid() )
				continue;

			try
			{
				var json = System.Text.Json.Nodes.JsonNode.Parse( kvp.Value );
				if ( json is System.Text.Json.Nodes.JsonObject jso )
				{
					var restored = new GameObject();
					restored.Deserialize( jso );
					restoredCount++;
				}
			}
			catch ( System.Exception ex )
			{
				Log.Warning( $"CleanupSystem: Failed to restore object {id}: {ex.Message}" );
			}
		}

		BroadcastCleanup( removedCount, restoredCount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void BroadcastCleanup( int removedObjects, int restoredObjects )
	{
		Game.ActiveScene?.RunEvent<ICleanupEvents>( x => x.OnCleanup( removedObjects, restoredObjects ) );

		Log.Info( $"Cleanup complete. Removed {removedObjects} spawned objects, restored {restoredObjects} destroyed objects." );
	}

	/// <summary>
	/// Console command to cleanup the map.
	/// </summary>
	[ConCmd( "cleanup" )]
	public static void CleanupCommand()
	{
		if ( Current is null )
		{
			Log.Warning( "CleanupSystem: No active cleanup system." );
			return;
		}

		Current.Cleanup();
	}
}
