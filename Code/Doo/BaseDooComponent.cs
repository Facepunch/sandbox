/// <summary>
/// Base component that can run Doo scripts on a GameObject.
/// </summary>
public class BaseDooComponent : Component
{
	/// <summary>
	/// A list of running Doos. This is lazily created, so will be null
	/// if no Doos are running.
	/// </summary>
	internal List<Doo.RunContext> _activeDoos;

	protected override void OnDisabled()
	{
		base.OnDisabled();

		StopAll();
	}

	/// <summary>
	/// Starts executing the given Doo on this component. Optionally configure initial arguments via the callback.
	/// </summary>
	public void Run( Doo doo, Action<Doo.Configure> c = null )
	{
		DooEngine
			.Get( Scene )
			.Run( this, doo, c );
	}

	/// <summary>
	/// Stop a specific Doo, if it's running
	/// </summary>
	public void Stop( Doo doo )
	{
		if ( _activeDoos == null ) return;
		if ( doo is null ) return;

		for ( int i = _activeDoos.Count - 1; i >= 0; i-- )
		{
			if ( _activeDoos[i].Doo != doo ) continue;

			_activeDoos[i].Stopped = true;
		}
	}

	/// <summary>
	/// Stop all running Doos
	/// </summary>
	public void StopAll()
	{
		if ( _activeDoos == null ) return;

		for ( int i = _activeDoos.Count - 1; i >= 0; i-- )
		{
			_activeDoos[i].Stopped = true;
		}
	}

	/// <summary>
	/// Returns true if the given Doo is currently running on this component.
	/// </summary>
	public bool IsRunning( Doo doo )
	{
		if ( _activeDoos == null ) return false;

		for ( int i = _activeDoos.Count - 1; i >= 0; i-- )
		{
			if ( _activeDoos[i].Stopped ) continue;
			if ( _activeDoos[i].Doo == doo ) return true;
		}

		return false;
	}
}
