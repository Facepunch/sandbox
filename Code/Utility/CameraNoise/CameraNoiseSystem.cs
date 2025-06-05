namespace Sandbox.CameraNoise;

public class CameraNoiseSystem : GameObjectSystem<CameraNoiseSystem>, ICameraSetup
{
	List<BaseCameraNoise> _all = new();

	public CameraNoiseSystem( Scene scene ) : base( scene )
	{
	}

	void ICameraSetup.PreSetup( Sandbox.CameraComponent cc )
	{
		foreach ( var effect in _all )
		{
			effect.Update();
		}

		_all.RemoveAll( x => x.IsDone );
	}

	void ICameraSetup.PostSetup( CameraComponent cc )
	{
		foreach ( var effect in _all )
		{
			effect.ModifyCamera( cc );
		}
	}

	public void Add( BaseCameraNoise noise )
	{
		_all.Add( noise );
	}
}

public abstract class BaseCameraNoise
{
	public float LifeTime { get; protected set; }
	public float CurrentTime { get; protected set; }
	public float Delta => CurrentTime.LerpInverse( 0, LifeTime, true );
	public float DeltaInverse => 1 - Delta;

	public BaseCameraNoise()
	{
		CameraNoiseSystem.Current.Add( this );
	}

	public virtual bool IsDone => CurrentTime > LifeTime;

	public virtual void Update()
	{
		CurrentTime += Time.Delta;
	}

	public virtual void ModifyCamera( CameraComponent cc ) { }
}
