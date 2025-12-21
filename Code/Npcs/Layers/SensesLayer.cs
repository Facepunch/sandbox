namespace Sandbox.Npcs.Layers;

/// <summary>
/// Handles awareness and environmental scanning
/// </summary>
public class SensesLayer : BaseNpcLayer
{
	public float ScanInterval { get; set; } = 0.1f; // Scan every 100ms
	public float SightRange { get; set; } = 500f;
	public float HearingRange { get; set; } = 300f;
	public float PersonalSpace { get; set; } = 80f;
	public TagSet TargetTags { get; set; } = ["player"];

	// Current awareness state
	public GameObject Nearest { get; private set; }
	public float DistanceToNearest { get; private set; } = float.MaxValue;
	public List<GameObject> VisibleTargets { get; private set; } = new();
	public List<GameObject> AudibleTargets { get; private set; } = new();

	private TimeSince _lastScan;

	protected override void OnUpdate()
	{
		if ( _lastScan > ScanInterval )
		{
			ScanEnvironment();
			_lastScan = 0;
		}
	}

	/// <summary>
	/// Scan for objects of interest
	/// </summary>
	private void ScanEnvironment()
	{
		// Clear previous scan results
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		Nearest = null;
		DistanceToNearest = float.MaxValue;

		// Find all potential targets in hearing range
		var nearbyObjects = Npc.Scene.FindInPhysics( new Sphere( Npc.WorldPosition, HearingRange ) );

		foreach ( var obj in nearbyObjects )
		{
			if ( !obj.Tags.HasAny( TargetTags ) ) continue;

			var distance = Npc.WorldPosition.Distance( obj.WorldPosition );

			// Track nearest
			if ( distance < DistanceToNearest )
			{
				DistanceToNearest = distance;
				Nearest = obj;
			}

			// Check if within hearing range
			if ( distance <= HearingRange )
			{
				AudibleTargets.Add( obj );
			}

			// Check if within sight range and has line of sight
			if ( distance <= SightRange && HasLineOfSight( obj ) )
			{
				VisibleTargets.Add( obj );
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
	/// Get the nearest visible
	/// </summary>
	public GameObject GetNearestVisible()
	{
		GameObject nearest = null;
		float nearestDistance = float.MaxValue;

		foreach ( var target in VisibleTargets )
		{
			var distance = Npc.WorldPosition.Distance( target.WorldPosition );
			if ( distance < nearestDistance )
			{
				nearestDistance = distance;
				nearest = target;
			}
		}

		return nearest;
	}

	public override void Reset()
	{
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		Nearest = null;
		DistanceToNearest = float.MaxValue;
	}
}
