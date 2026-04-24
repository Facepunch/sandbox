using Sandbox.UI;

[Hide]
[Title( "Wheel" )]
[Icon( "🛞" )]
[ClassName( "wheeltool" )]
[Group( "Building" )]
public class WheelTool : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];
	[Property, ResourceSelect( Extension = "wdef", AllowPackages = true ), Title( "Wheel" )]
	public string Definition { get; set; } = "entities/wheel/basic.wdef";

	public override string Description => "#tool.hint.wheeltool.description";
	public override string PrimaryAction => "#tool.hint.wheeltool.place";
	public override string SecondaryAction => "#tool.hint.wheeltool.toggle_axis";
	public override string ReloadAction => "#tool.hint.wheeltool.toggle_direction";

	Vector3 _axis = Vector3.Right;
	bool _reversed = false;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var def = ResourceLibrary.Get<WheelDefinition>( Definition );
		if ( def == null || def.Prefab?.GetScene() is not Scene scene ) return;

		var pos = select.WorldTransform();
		var modelBounds = scene.GetBounds();
		var surfaceOffset = modelBounds.Size.y * 0.5f;

		if ( Input.Pressed( "attack2" ) )
		{
			_axis = _axis == Vector3.Right ? Vector3.Up : Vector3.Right;
		}

		if ( Input.Pressed( "reload" ) )
		{
			_reversed = !_reversed;
		}

		var placementTrans = new Transform( pos.Position + pos.Rotation.Forward * surfaceOffset );
		placementTrans.Rotation = Rotation.LookAt( pos.Rotation.Forward, pos.Rotation * _axis ) * new Angles( 0, 90, 0 );
		placementTrans.Scale = scene.LocalScale;

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnWheel( select, def, placementTrans, _reversed );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( scene, transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );

		var steerAngle = MathF.Sin( Time.Now * 2f ) * 25f;
		var steer = Rotation.FromAxis( placementTrans.Forward, steerAngle );
		var cylRadius = MathF.Max( modelBounds.Size.x, modelBounds.Size.z ) * 0.5f * placementTrans.Scale.x + 0.5f;
		var cylAxis = steer * placementTrans.Right;
		DebugOverlay.Cylinder( new Capsule( placementTrans.Position - cylAxis * 2f, placementTrans.Position + cylAxis * 2f, cylRadius ), Color.Yellow, segments: 32 );

		var overlayRadius = MathF.Max( modelBounds.Size.x, modelBounds.Size.z ) * 0.01f * placementTrans.Scale.x;
		WheelOverlay.DrawDirection( placementTrans.Position, placementTrans.Right, pos.Rotation.Up, overlayRadius, _reversed );
	}

	[Rpc.Host]
	public void SpawnWheel( SelectionPoint point, WheelDefinition def, Transform tx, bool reversed )
	{
		if ( def == null || def.Prefab?.GetScene() is not Scene scene ) return;

		var wheelGo = scene.Clone( new CloneConfig { StartEnabled = false } );
		wheelGo.Name = "wheel";
		wheelGo.Tags.Add( "removable" );
		wheelGo.Tags.Add( "constraint" );
		wheelGo.WorldTransform = tx;

		var we = wheelGo.GetOrAddComponent<WheelEntity>();
		we.Reversed = reversed;
		var joint = wheelGo.GetComponentInChildren<WheelJoint>( true );

		if ( joint is null )
		{
			var wheelAnchor = new GameObject( true, "anchor2" );
			wheelAnchor.Parent = wheelGo;
			wheelAnchor.LocalRotation = new Angles( 0, 90, 90 );

			//var joint = jointGo.AddComponent<HingeJoint>();
			joint = wheelAnchor.AddComponent<WheelJoint>();
			joint.Attachment = Joint.AttachmentMode.Auto;
			joint.EnableSuspension = true;
			joint.EnableSuspensionLimit = true;
			joint.SuspensionLimits = new Vector2( -32, 32 );
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( wheelGo );

		joint.Body = point.GameObject;

		wheelGo.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Wheel";
		undo.Add( wheelGo );

		CheckContraptionStats( point.GameObject );
	}
}
