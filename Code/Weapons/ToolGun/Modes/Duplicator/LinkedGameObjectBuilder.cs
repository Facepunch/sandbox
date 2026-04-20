

public class LinkedGameObjectBuilder
{
	public List<GameObject> Objects { get; } = new();

	/// <summary>
	/// Reject players and any objects with players as descendants. This is used for the duplicator to avoid accidentally duping the player and all their attachments.
	/// </summary>
	public bool RejectPlayers { get; set; } = false;

	/// <summary>
	/// Adds a GameObject. Won't find connections.
	/// </summary>
	public bool Add( GameObject obj )
	{
		if ( !obj.IsValid() ) return false;
		if ( obj.Tags.Contains( "world" ) ) return false;
		if ( Objects.Contains( obj ) ) return false;
		if ( obj.GetComponent<MapInstance>() is not null ) return false;
		if ( RejectPlayers && HasDescendantWithTag( obj, "player" ) ) return false;

		Objects.Add( obj );
		return true;
	}

	/// <summary>
	/// Add a GameObject with all connected GameObjects
	/// </summary>
	public void AddConnected( GameObject source )
	{
		if ( !source.IsValid() ) return;

		//
		// we're only interested in root objects
		//
		source = source.Root;

		// If we can't add this then don't add children
		// because we must have already added it, or it's the world.
		if ( !Add( source ) ) return;

		foreach ( var rb in source.GetComponentsInChildren<Rigidbody>() )
		{
			foreach ( var joint in rb.Joints )
			{
				AddConnected( joint.Object1 );
				AddConnected( joint.Object2 );
			}
		}

		foreach ( var collider in source.GetComponentsInChildren<Collider>() )
		{
			foreach ( var joint in collider.Joints )
			{
				AddConnected( joint.Object1 );
				AddConnected( joint.Object2 );
			}
		}

		foreach ( var link in source.GetComponentsInChildren<ManualLink>() )
		{
			if ( link.Body.IsValid() )
				AddConnected( link.Body );
		}

		// If any children have a physics filter, also connect them.
		foreach ( var filter in source.GetComponentsInChildren<PhysicsFilter>( includeSelf: false ) )
		{
			AddConnected( filter.GameObject );
		}
	}

	public void RemoveDeletedObjects()
	{
		Objects.RemoveAll( x => !x.IsValid() || x.IsDestroyed );
	}

	static bool HasDescendantWithTag( GameObject obj, string tag )
	{
		foreach ( var child in obj.Children )
		{
			if ( child.Tags.Has( tag ) ) return true;
			if ( HasDescendantWithTag( child, tag ) ) return true;
		}
		return false;
	}

	public void Clear()
	{
		Objects.Clear();
	}
}
