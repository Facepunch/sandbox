namespace Sandbox;

/// <summary>
/// An actor, could be a player or a NPC - maybe should just be a base class instead 
/// </summary>
public interface IActor : IValid
{
	Transform EyeTransform { get; }
	GameObject GameObject { get; }
	Vector3 WorldPosition { get; }
	T GetComponent<T>( bool includeDisabled = false );
	T GetComponentInParent<T>( bool includeDisabled = false, bool includeSelf = true );
}
