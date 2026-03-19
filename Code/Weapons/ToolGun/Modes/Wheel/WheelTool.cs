﻿
using Sandbox.UI;

[Hide]
[Title( "Wheel" )]
[Icon( "🛞" )]
[ClassName( "wheeltool" )]
[Group( "Building" )]
public class WheelTool : ToolMode
{
	public override bool UseSnapGrid => true;
	[Property, ResourceSelect( Extension = "wdef", AllowPackages = true ), Title( "Wheel" )]
	public string Definition { get; set; } = "entities/wheel/basic.wdef";

	public override string Description => "#tool.hint.wheeltool.description";
	public override string PrimaryAction => "#tool.hint.wheeltool.place";
	public override string SecondaryAction => "#tool.hint.wheeltool.toggle_axis";

	Vector3 _axis = Vector3.Right;

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

		var placementTrans = new Transform( pos.Position + pos.Rotation.Forward * surfaceOffset );
		placementTrans.Rotation = Rotation.LookAt( pos.Rotation.Forward, pos.Rotation * _axis ) * new Angles( 0, 90, 0 );
		placementTrans.Scale = scene.LocalScale;

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnWheel( select, def, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( scene, transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
		DebugOverlay.Line( new Line( placementTrans.Position, placementTrans.Position + placementTrans.Right * 5 ), Color.White );

		var suspensionAxis = placementTrans.Forward * 20;
		DebugOverlay.Line( new Line( placementTrans.Position - suspensionAxis, placementTrans.Position + suspensionAxis ), Color.Green );

	}

	[Rpc.Host]
	public void SpawnWheel( SelectionPoint point, WheelDefinition def, Transform tx )
	{
		if ( def == null || def.Prefab?.GetScene() is not Scene scene ) return;

		var wheelGo = scene.Clone( new CloneConfig { StartEnabled = false } );
		wheelGo.Name = "wheel";
		wheelGo.Tags.Add( "removable" );
		wheelGo.WorldTransform = tx;

		var we = wheelGo.GetOrAddComponent<WheelEntity>();
		var joint = wheelGo.GetComponentInChildren<WheelJoint>( true );

		if ( joint == null )
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

		joint.Body = point.GameObject;

		wheelGo.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Wheel";
		undo.Add( wheelGo );
	}

}
