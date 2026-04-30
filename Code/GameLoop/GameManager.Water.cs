using Sandbox.UI;

public partial class WaterVolume : Component, Component.ITriggerListener
{
	List<Rigidbody> Bodies = new();

	protected override void OnFixedUpdate()
	{
		if ( Bodies is null )
			return;

		var collider = GetComponent<BoxCollider>();
		var waterSurface = WorldPosition + Vector3.Up * (collider.Scale.z * 0.5f);
		var waterPlane = new Plane( waterSurface, Vector3.Up );

		for ( int i = Bodies.Count - 1; i >= 0; i-- )
		{
			var body = Bodies[i];
			if ( !body.IsValid() )
			{
				Bodies.RemoveAt( i );
				continue;
			}

			body.ApplyBuoyancy( waterPlane, Time.Delta );
		}
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var body = other.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent );
		if ( body.IsValid() && !Bodies.Contains( body ) )
		{
			Bodies.Add( body );
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		var body = other.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent );
		if ( body.IsValid() )
		{
			Bodies.Remove( body );
		}
	}
}

public sealed partial class GameManager : ISceneLoadingEvents
{
	void ISceneLoadingEvents.AfterLoad( Scene scene )
	{
		var waterVolumes = scene.GetAll<Collider>().Where( x => x.Tags.Has( "water" ) );
		if ( waterVolumes.Count() < 1 ) return;

		foreach ( var volume in waterVolumes )
		{
			volume.GetOrAddComponent<WaterVolume>();
		}
	}
}
