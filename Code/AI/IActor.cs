namespace Sandbox;

/// <summary>
/// An actor, could be a player or a NPC - maybe should just be a base class instead 
/// </summary>
public interface IActor : IValid
{
	public GameObject GameObject { get; }
	public Vector3 WorldPosition { get; }
	public T GetComponent<T>( bool includeDisabled = false );
	public T GetComponentInParent<T>( bool includeDisabled = false, bool includeSelf = true );
}
