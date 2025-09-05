

public class LinkedGameObjectBuilder
{
	public List<GameObject> Objects { get; } = new();

	/// <summary>
	/// Adds a GameObject. Won't find connections.
	/// </summary>
	public bool Add( GameObject obj )
	{
		if ( !obj.IsValid() ) return false;
		if ( obj.Tags.Contains( "world" ) ) return false;
		if ( Objects.Contains( obj ) ) return false;

		Objects.Add( obj );
		return true;
	}

	/// <summary>
	/// Add a GameObject with all connected GameObjects
	/// </summary>
	public void AddConnected( GameObject source )
	{
		// Use iterative approach to avoid stack overflow with complex object hierarchies
		var toProcess = new Queue<GameObject>();
		toProcess.Enqueue( source );

		while ( toProcess.Count > 0 )
		{
			var current = toProcess.Dequeue();
			
			// We're only interested in root objects
			current = current.Root;

			// If we can't add this then skip it
			// because we must have already added it, or it's the world.
			if ( !Add( current ) ) continue;

			// Find all connected objects through joints
			AddConnectedObjectsToQueue( current, toProcess );
		}
	}

	/// <summary>
	/// Helper method to find connected objects and add them to the processing queue
	/// </summary>
	private void AddConnectedObjectsToQueue( GameObject obj, Queue<GameObject> queue )
	{
		// Check rigidbody joints
		foreach ( var rb in obj.GetComponents<Rigidbody>() )
		{
			foreach ( var joint in rb.Joints )
			{
				if ( joint.Object1.IsValid() && joint.Object1 != obj )
					queue.Enqueue( joint.Object1 );
				if ( joint.Object2.IsValid() && joint.Object2 != obj )
					queue.Enqueue( joint.Object2 );
			}
		}

		// Check collider joints
		foreach ( var collider in obj.GetComponents<Collider>() )
		{
			foreach ( var joint in collider.Joints )
			{
				if ( joint.Object1.IsValid() && joint.Object1 != obj )
					queue.Enqueue( joint.Object1 );
				if ( joint.Object2.IsValid() && joint.Object2 != obj )
					queue.Enqueue( joint.Object2 );
			}
		}
	}

	public void RemoveDeletedObjects()
	{
		Objects.RemoveAll( x => !x.IsValid() || x.IsDestroyed );
	}

	public void Clear()
	{
		Objects.Clear();
	}
}
