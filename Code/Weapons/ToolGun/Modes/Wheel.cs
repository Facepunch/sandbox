
[Icon( "🛞" )]
[ClassName( "wheel" )]
[Group( "Building" )]
public class Wheel : ToolMode
{
	Model wheelModel = Cloud.Model( "facepunch/tyre_with_rim" );

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();
		var modelBounds = wheelModel.Bounds;
		var surfaceOffset = modelBounds.Size.y * 0.5f;

		var placementTrans = new Transform( pos.Position + pos.Rotation.Forward * surfaceOffset );
		placementTrans.Rotation = Rotation.LookAt( pos.Rotation.Forward, Vector3.Up ) * new Angles( 0, 90, 0 );

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnWheel( select, wheelModel, placementTrans );
		}

		DebugOverlay.Model( wheelModel, transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );

	}

	[Rpc.Host]
	public void SpawnWheel( SelectionPoint point, Model model, Transform tx )
	{
		var wheelGo = new GameObject( false, "wheel" );
		wheelGo.Tags.Add( "removable" );
		wheelGo.WorldTransform = tx;

		var wheelProp = wheelGo.AddComponent<Prop>();
		wheelProp.Model = model;

		var wheelAnchor = new GameObject( false, "anchor2" );
		wheelAnchor.Parent = wheelGo;
		wheelAnchor.LocalRotation = Rotation.FromRoll( 90 );

		var jointGo = new GameObject( false, "anchor1" );
		jointGo.Parent = point.GameObject;
		jointGo.WorldTransform = wheelAnchor.WorldTransform;

		var joint = jointGo.AddComponent<HingeJoint>();
		joint.Attachment = Joint.AttachmentMode.Auto;
		joint.Body = wheelAnchor;
		joint.EnableCollision = false;

		wheelGo.NetworkSpawn( true, null );
		jointGo.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Add( wheelGo );
		undo.Add( jointGo );
	}

}
