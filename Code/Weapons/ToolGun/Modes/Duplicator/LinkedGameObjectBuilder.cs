

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
		var toProcess = new Queue<GameObject>();
		toProcess.Enqueue( source );

		while ( toProcess.Count > 0 )
		{
			var current = toProcess.Dequeue();
			current = current.Root;

			if ( !Add( current ) ) continue;

			foreach ( var rb in current.GetComponents<Rigidbody>() )
			{
				foreach ( var joint in rb.Joints )
				{
					toProcess.Enqueue( joint.Object1 );
					toProcess.Enqueue( joint.Object2 );
				}
			}

			foreach ( var collider in current.GetComponents<Collider>() )
			{
				foreach ( var joint in collider.Joints )
				{
					toProcess.Enqueue( joint.Object1 );
					toProcess.Enqueue( joint.Object2 );
				}
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
