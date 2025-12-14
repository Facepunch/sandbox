[Title( "Thruster" )]
[Icon( "🚀" )]
[ClassName( "thruster" )]
[Group( "Building" )]
public class ThrusterTool : ToolMode
{
	Model wheelModel = Cloud.Model( "facepunch.soda_can" );

	Vector3 _axis = Vector3.Right;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();

		if ( Input.Pressed( "attack2" ) )
		{
			_axis = _axis == Vector3.Right ? Vector3.Up : Vector3.Right;
		}

		var placementTrans = new Transform( pos.Position );
		placementTrans.Rotation = pos.Rotation * new Angles( 90, 0, 0 );

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnWheel( select, wheelModel, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.Model( wheelModel, transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );

	}

	[Rpc.Host]
	public void SpawnWheel( SelectionPoint point, Model model, Transform tx )
	{
		var go = new GameObject( false, "thruster" );
		go.Tags.Add( "removable" );
		go.WorldTransform = tx;

		var thuster = go.AddComponent<Thruster>();

		var prop = go.AddComponent<Prop>();
		prop.Model = model;

		//var joint = jointGo.AddComponent<HingeJoint>();
		var joint = thuster.AddComponent<FixedJoint>();
		joint.Attachment = Joint.AttachmentMode.LocalFrames;
		joint.LocalFrame2 = point.GameObject.WorldTransform.ToLocal( tx );
		joint.LocalFrame1 = new Transform();
		joint.AngularFrequency = 0;
		joint.LinearFrequency = 0;
		joint.Body = point.GameObject;
		joint.EnableCollision = false;

		go.NetworkSpawn( true, null );

		// undo
		{
			var undo = Player.Undo.Create();
			undo.Name = "Thruster";
			undo.Icon = "🚀";
			undo.Add( go );
		}
	}

}
