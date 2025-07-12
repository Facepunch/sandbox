
[Icon( "🎱" )]
[ClassName( "ballsocket" )]
public class BallSocket : Constraint
{
	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go1 = new GameObject( false, "ballsocket" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;

		var go2 = new GameObject( false, "ballsocket" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;

		var joint = go1.AddComponent<BallJoint>();
		joint.Body = go2;
		joint.Friction = 0.0f;
		joint.EnableCollision = false;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
