public sealed partial class Player
{
	[Property] float SprayTime { get; set; } = 5;

	TimeUntil _timeUntilSprayAllowed { get; set; }

	void ControlSpray()
	{
		if ( !Input.Pressed( "Spray" ) )
			return;

		if ( _timeUntilSprayAllowed > 0 ) return;

		var tr = Scene.Trace.Ray( EyeTransform.Position, EyeTransform.Position + EyeTransform.Forward * 200 )
			.IgnoreGameObject( GameObject )
			.WithoutTags( "player" )
			.Run();

		if ( !tr.Hit ) return;

		_timeUntilSprayAllowed = SprayTime;
		Spray();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void Spray()
	{
		var sprayPrefab = GameObject.GetPrefab( "items/spray/player_spray.prefab" );

		var tr = Scene.Trace.Ray( EyeTransform.Position, EyeTransform.Position + EyeTransform.Forward * 200 )
			.IgnoreGameObject( GameObject )
			.WithoutTags( "player" )
			.Run();

		if ( !tr.Hit ) return;

		var sprays = Scene.GetAllComponents<Spray>().Where( x => x.Network.Owner == Network.Owner );
		foreach ( var spray in sprays )
		{
			spray.GameObject.Destroy();
		}

		if ( sprayPrefab.IsValid() )
		{
			var _spray = sprayPrefab.Clone( tr.HitPosition + tr.Normal * 5, Rotation.LookAt( -tr.Normal ) );
			_spray.NetworkSpawn( Rpc.Caller );
		}
	}
}
