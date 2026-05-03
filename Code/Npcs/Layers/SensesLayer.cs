namespace Sandbox.Npcs.Layers;

/// <summary>
/// Handles awareness and environmental scanning.
/// Scans for all objects matching <see cref="ScanTags"/> and caches them by tag.
/// <see cref="TargetTags"/> filters which cached objects are treated as hostile targets.
/// </summary>
public class SensesLayer : BaseNpcLayer
{
	public float ScanInterval { get; set; } = 0.1f; // Scan every 100ms

	[Property]
	public float SightRange { get; set; } = 500f;

	[Property]
	public float HearingRange { get; set; } = 300f;
	public float PersonalSpace { get; set; } = 80f;

	/// <summary>
	/// All tags the NPC should scan for and cache. Results are bucketed by tag.
	/// </summary>
	[Property]
	public TagSet ScanTags { get; set; } = ["player"];

	/// <summary>
	/// Tags that are treated as hostile targets. <see cref="VisibleTargets"/> and
	/// <see cref="AudibleTargets"/> only contain objects matching these tags.
	/// </summary>
	[Property]
	public TagSet TargetTags { get; set; } = ["player"];

	// Hostile-only lists (backward compat)
	public GameObject Nearest { get; private set; }
	public float DistanceToNearest { get; private set; } = float.MaxValue;
	public List<GameObject> VisibleTargets { get; private set; } = new();
	public List<GameObject> AudibleTargets { get; private set; } = new();

	// Tag-bucketed caches
	private readonly Dictionary<string, List<GameObject>> _visibleByTag = new();
	private readonly Dictionary<string, List<GameObject>> _audibleByTag = new();

	private TimeSince _lastScan;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( _lastScan > ScanInterval )
		{
			ScanEnvironment();
			_lastScan = 0;
		}
	}

	public override string GetDebugString()
	{
		if ( VisibleTargets.Count == 0 && AudibleTargets.Count == 0 ) return null;
		return $"Senses: {VisibleTargets.Count} visible, {AudibleTargets.Count} audible";
	}

	/// <summary>
	/// Scan for all objects matching <see cref="ScanTags"/>, bucket by tag,
	/// and populate the hostile-filtered <see cref="VisibleTargets"/>/<see cref="AudibleTargets"/>.
	/// </summary>
	private void ScanEnvironment()
	{
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		ClearTagCache( _visibleByTag );
		ClearTagCache( _audibleByTag );
		Nearest = null;
		DistanceToNearest = float.MaxValue;

		if ( NpcConVars.NoTarget )
			return;

		var nearbyObjects = Npc.Scene.FindInPhysics( new Sphere( Npc.WorldPosition, HearingRange ) );

		foreach ( var obj in nearbyObjects )
		{
			if ( !obj.Tags.HasAny( ScanTags ) ) continue;

			var distance = Npc.WorldPosition.Distance( obj.WorldPosition );
			bool isTarget = obj.Tags.HasAny( TargetTags );

			// Track nearest hostile target
			if ( isTarget && distance < DistanceToNearest )
			{
				DistanceToNearest = distance;
				Nearest = obj;
			}

			bool isAudible = distance <= HearingRange;
			bool isVisible = distance <= SightRange && HasLineOfSight( obj );

			if ( isAudible )
			{
				AddToTagCache( _audibleByTag, obj );
				if ( isTarget ) AudibleTargets.Add( obj );
			}

			if ( isVisible )
			{
				AddToTagCache( _visibleByTag, obj );
				if ( isTarget ) VisibleTargets.Add( obj );
			}
		}
	}

	/// <summary>
	/// Check if we have line of sight to target
	/// </summary>
	private bool HasLineOfSight( GameObject target )
	{
		var eyePosition = Npc.WorldPosition + Vector3.Up * 64f; // Eye height
		var targetPosition = target.WorldPosition + Vector3.Up * 32f; // Target center

		var trace = Npc.Scene.Trace.Ray( eyePosition, targetPosition )
			.IgnoreGameObjectHierarchy( Npc.GameObject )
			.WithoutTags( "trigger" )
			.Run();

		return !trace.Hit || trace.GameObject == target || target.IsDescendant( trace.GameObject );
	}

	/// <summary>
	/// Get the nearest visible hostile target (matching <see cref="TargetTags"/>).
	/// </summary>
	public GameObject GetNearestVisible()
	{
		return GetNearestIn( VisibleTargets );
	}

	/// <summary>
	/// Get the nearest visible object with a specific tag.
	/// </summary>
	public GameObject GetNearestVisible( string tag )
	{
		return GetNearestIn( GetVisible( tag ) );
	}

	/// <summary>
	/// Get all visible objects with a specific tag from the cache.
	/// </summary>
	public List<GameObject> GetVisible( string tag )
	{
		return _visibleByTag.TryGetValue( tag, out var list ) ? list : _empty;
	}

	/// <summary>
	/// Get all audible objects with a specific tag from the cache.
	/// </summary>
	public List<GameObject> GetAudible( string tag )
	{
		return _audibleByTag.TryGetValue( tag, out var list ) ? list : _empty;
	}

	public override void ResetLayer()
	{
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		ClearTagCache( _visibleByTag );
		ClearTagCache( _audibleByTag );
		Nearest = null;
		DistanceToNearest = float.MaxValue;
	}

	private GameObject GetNearestIn( List<GameObject> list )
	{
		GameObject nearest = null;
		float nearestDist = float.MaxValue;

		foreach ( var obj in list )
		{
			var dist = Npc.WorldPosition.Distance( obj.WorldPosition );
			if ( dist < nearestDist )
			{
				nearestDist = dist;
				nearest = obj;
			}
		}

		return nearest;
	}

	private static void AddToTagCache( Dictionary<string, List<GameObject>> cache, GameObject obj )
	{
		foreach ( var tag in obj.Tags )
		{
			if ( !cache.TryGetValue( tag, out var list ) )
			{
				list = new List<GameObject>();
				cache[tag] = list;
			}
			list.Add( obj );
		}
	}

	private static void ClearTagCache( Dictionary<string, List<GameObject>> cache )
	{
		foreach ( var list in cache.Values )
			list.Clear();
	}

	private static readonly List<GameObject> _empty = new();
}
