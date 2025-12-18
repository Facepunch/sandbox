
[Icon( "🛞" )]
[ClassName( "wheel" )]
[Group( "Building" )]
public class Wheel : ToolMode
{
	Model wheelModel = Cloud.Model( "facepunch/tyre_with_rim" );

	Vector3 _axis = Vector3.Right;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();
		var modelBounds = wheelModel.Bounds;
		var surfaceOffset = modelBounds.Size.y * 0.5f;

		if ( Input.Pressed( "attack2" ) )
		{
			_axis = _axis == Vector3.Right ? Vector3.Up : Vector3.Right;
		}

		var placementTrans = new Transform( pos.Position + pos.Rotation.Forward * surfaceOffset );
		placementTrans.Rotation = Rotation.LookAt( pos.Rotation.Forward, pos.Rotation * _axis ) * new Angles( 0, 90, 0 );

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnWheel( select, wheelModel, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.Model( wheelModel, transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );

		DebugOverlay.Line( new Line( placementTrans.Position, placementTrans.Position + placementTrans.Right * 5 ), Color.White );

		var suspensionAxis = placementTrans.Forward * 20;
		DebugOverlay.Line( new Line( placementTrans.Position - suspensionAxis, placementTrans.Position + suspensionAxis ), Color.Green );

	}

	[Rpc.Host]
	public void SpawnWheel( SelectionPoint point, Model model, Transform tx )
	{
		var wheelGo = new GameObject( false, "wheel" );
		wheelGo.Tags.Add( "removable" );
		wheelGo.WorldTransform = tx;

		var wheelProp = wheelGo.AddComponent<Prop>();
		wheelProp.Model = model;

		var wheelAnchor = new GameObject( true, "anchor2" );
		wheelAnchor.Parent = wheelGo;
		wheelAnchor.LocalRotation = new Angles( 0, 90, 90 );

		//var joint = jointGo.AddComponent<HingeJoint>();
		var joint = wheelAnchor.AddComponent<WheelJoint>();
		joint.Attachment = Joint.AttachmentMode.Auto;
		joint.EnableSuspension = true;
		joint.EnableSuspensionLimit = true;
		joint.SuspensionLimits = new Vector2( -32, 32 );
		joint.Body = point.GameObject;
		joint.EnableCollision = false;

		joint.AddComponent<EditableWheel>();

		wheelGo.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Wheel";
		undo.Add( wheelGo );
	}

}
