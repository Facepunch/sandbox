
[Icon( "🥽" )]
[ClassName( "weld" )]
public class Weld : ToolMode
{

	SelectionPoint _point1;
	SelectionPoint _point2;
	int stage = 0;

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Pressed( "attack1" ) )
		{
			var select = TraceSelect();

			if ( select.GameObject is null )
				return;

			if ( stage == 0 )
			{
				_point1 = select;
				ShootEffects( select );
				stage++;
				return;
			}

			if ( stage == 1 )
			{
				_point2 = select;
				CreateJoint( _point1, _point2 );
				ShootEffects( select );
			}

			stage = 0;

		}
	}

	[Rpc.Host]
	private void CreateJoint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go1 = new GameObject( false, "weld" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.LocalRotation = Rotation.Identity;

		var go2 = new GameObject( false, "weld" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.LocalRotation = Rotation.Identity;

		var len = point1.WorldPosition().Distance( point2.WorldPosition() );

		var fixedJoint = go1.AddComponent<FixedJoint>();
		fixedJoint.Attachment = Joint.AttachmentMode.Auto;
		fixedJoint.Body = go2;
		fixedJoint.EnableCollision = true;
		fixedJoint.AngularFrequency = 10;
		fixedJoint.LinearFrequency = 10;

		go2.NetworkSpawn();
		go1.NetworkSpawn();
	}
}
