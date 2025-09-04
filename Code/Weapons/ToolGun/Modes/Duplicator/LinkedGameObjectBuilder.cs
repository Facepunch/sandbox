

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
		//
		// we're only interested in root objects
		//
		source = source.Root;

		// If we can't add this then don't add children
		// because we must have already added it, or it's the world.
		if ( !Add( source ) ) return;

		foreach ( var rb in source.GetComponents<Rigidbody>() )
		{
			foreach ( var joint in rb.Joints )
			{
				AddConnected( joint.Object1 );
				AddConnected( joint.Object2 );
			}
		}

		foreach ( var collider in source.GetComponents<Collider>() )
		{
			foreach ( var joint in collider.Joints )
			{
				AddConnected( joint.Object1 );
				AddConnected( joint.Object2 );
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
