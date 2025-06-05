
/// <summary>
/// Creates a bunch of callbacks, allowing finer control over applying camera effects
/// </summary>
public sealed class CameraSetup : Component
{
	protected override void OnPreRender()
	{
		var cc = GetComponent<CameraComponent>();
		if ( cc is null ) return;

		ICameraSetup.Post( x => x.PreSetup( cc ) );
		ICameraSetup.Post( x => x.Setup( cc ) );
		ICameraSetup.Post( x => x.PostSetup( cc ) );
	}
}


public interface ICameraSetup : ISceneEvent<ICameraSetup>
{
	// Effects before viewmodel
	public void PreSetup( CameraComponent cc ) { }

	// Place viewmodel
	public void Setup( CameraComponent cc ) { }

	// Effects including viewmodel
	public void PostSetup( CameraComponent cc ) { }
}
