
[Icon( "🛞" )]
[ClassName( "wheel" )]
public class Wheel : ToolMode
{
	Model wheelModel = Cloud.Model( "facepunch/tyre_with_rim" );

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();

		var pos = select.WorldTransform();
		var modelBounds = wheelModel.Bounds;
		var surfaceOffset = modelBounds.Size.y * 0.5f;

		var placementTrans = new Transform( pos.Position + pos.Rotation.Forward * surfaceOffset );
		placementTrans.Rotation = Rotation.LookAt( pos.Rotation.Forward, Vector3.Up ) * new Angles( 0, 90, 0 );

		if ( Input.Pressed( "attack1" ) )
		{
			if ( !select.IsValid() ) return;

			SpawnWheel( select, wheelModel, placementTrans );
		}

		DebugOverlay.Model( wheelModel, transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );

	}

	[Rpc.Host]
	public void SpawnWheel( SelectionPoint point, Model model, Transform tx )
	{
		var wheelGo = new GameObject( false, "wheel" );
		wheelGo.WorldTransform = tx;

		var wheelProp = wheelGo.AddComponent<Prop>();
		wheelProp.Model = model;

		var jointGo = new GameObject( false, "wheel-joint" );
		jointGo.Parent = point.GameObject;
		jointGo.LocalTransform = point.LocalTransform;

		var joint = jointGo.AddComponent<HingeJoint>();
		joint.Attachment = Joint.AttachmentMode.Auto;
		joint.Body = wheelGo;
		joint.Frequency = 0;
		joint.EnableCollision = false;

		wheelGo.NetworkSpawn( true, null );
		jointGo.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Add( wheelGo );
		undo.Add( jointGo );
	}

}
