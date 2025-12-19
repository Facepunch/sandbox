using Sandbox.UI;

[Hide]
[Title( "Thruster" )]
[Icon( "🚀" )]
[ClassName( "thrustertool" )]
[Group( "Building" )]
public class ThrusterTool : ToolMode
{
	[Property, ResourceSelect( Extension = "tdef", AllowPackages = true ), Title( "Thruster" )]
	public string Definition { get; set; } = "entities/thruster/basic.tdef";

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

		var thrusterDef = ResourceLibrary.Get<ThrusterDefinition>( Definition );
		if ( thrusterDef == null ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnWheel( select, thrusterDef.Prefab, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( thrusterDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );

	}

	[Rpc.Host]
	public void SpawnWheel( SelectionPoint point, PrefabFile thrusterPrefab, Transform tx )
	{
		if ( thrusterPrefab == null )
			return;

		var go = thrusterPrefab.GetScene().Clone();
		go.Tags.Add( "removable" );
		go.WorldTransform = tx;

		var thuster = go.GetComponent<ThrusterEntity>();

		if ( !point.GameObject.Tags.Contains( "world" ) )
		{
			// attach it
			var joint = thuster.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.ToLocal( tx );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
		}

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
