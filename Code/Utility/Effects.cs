namespace Sandbox;

public class Effects : GameObjectSystem<Effects>
{
	public List<Material> BloodDecalMaterials { get; set; } = new()
	{
		Cloud.Material( "jase.bloodsplatter08" ),
		Cloud.Material( "jase.bloodsplatter07" ),
		Cloud.Material( "jase.bloodsplatter06" ),
		Cloud.Material( "jase.bloodsplatter05" ),
		Cloud.Material( "jase.bloodsplatter04" )
	};

	public Effects( Scene scene ) : base( scene )
	{

	}

	public void SpawnBlood( Vector3 hitPosition, Vector3 direction, float damage = 50.0f )
	{
		const float BloodEjectDistance = 256.0f;
		var tr = Game.ActiveScene.Trace.Ray( new Ray( hitPosition, -direction ), BloodEjectDistance )
			.WithoutTags( "player" )
			.Run();

		if ( !tr.Hit ) return;

		var material = Random.Shared.FromList( BloodDecalMaterials );
		if ( !material.IsValid() ) return;

		var gameObject = Game.ActiveScene.CreateObject();
		gameObject.Name = "Blood splatter";
		gameObject.WorldPosition = tr.HitPosition + tr.Normal;
		gameObject.WorldRotation = Rotation.LookAt( -tr.Normal );
		gameObject.WorldRotation *= Rotation.FromAxis( Vector3.Forward, Game.Random.Float( 0, 360 ) );
	}

	public static void SpawnExplosion( Vector3 worldPosition )
	{
		var prefab = GameObject.GetPrefab( "/prefabs/effects/explosion.prefab" );
		prefab?.Clone( worldPosition );
	}

	public static void SpawnExplosionSmall( Vector3 worldPosition )
	{
		var prefab = GameObject.GetPrefab( "/prefabs/effects/explosion_sm.prefab" );
		prefab?.Clone( worldPosition );
	}

	public static void SpawnScorch( Vector3 worldPosition )
	{
		var prefab = GameObject.GetPrefab( "/prefabs/effects/scorchdecal.prefab" );
		prefab.Clone( worldPosition, Rotation.Random );
	}
}
