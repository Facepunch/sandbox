using System;

// This will be a part of Component when we ship

public class BaseDooComponent : Component
{
	public BaseDooComponent()
	{
	}

	public void Run( Doo doo, Action<Doo.Configure> c = null )
	{
		DooEngine
			.Get( Scene )
			.Run( this, doo, c );
	}
}
