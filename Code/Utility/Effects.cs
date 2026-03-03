namespace Sandbox;

public class Effects( Scene scene ) : GameObjectSystem<Effects>( scene )
{
	public List<Material> BloodDecalMaterials { get; set; } =
	[
		Cloud.Material( "jase.bloodsplatter08" ),
		Cloud.Material( "jase.bloodsplatter07" ),
		Cloud.Material( "jase.bloodsplatter06" ),
		Cloud.Material( "jase.bloodsplatter05" ),
		Cloud.Material( "jase.bloodsplatter04" )
	];

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
}
