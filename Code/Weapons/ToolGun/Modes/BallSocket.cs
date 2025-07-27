
[Icon( "🎱" )]
[ClassName( "ballsocket" )]
[Group( "Constraints" )]
public class BallSocket : BaseConstraintToolMode
{
	[Property, Sync]
	public bool EnableCollision { get; set; } = false;

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( point1.GameObject == point2.GameObject )
			return;

		var go2 = new GameObject( point2.GameObject, false, "ballsocket" );
		go2.LocalTransform = point2.LocalTransform;

		var go1 = new GameObject( point1.GameObject, false, "ballsocket" );
		go1.WorldTransform = go2.WorldTransform;

		var joint = go1.AddComponent<BallJoint>();
		joint.Body = go2;
		joint.Friction = 0.0f;
		joint.EnableCollision = EnableCollision;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
