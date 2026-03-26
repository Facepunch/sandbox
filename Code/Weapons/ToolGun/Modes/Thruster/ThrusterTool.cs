﻿using Sandbox.UI;

[Hide]
[Title( "Thruster" )]
[Icon( "🚀" )]
[ClassName( "thrustertool" )]
[Group( "Building" )]
public class ThrusterTool : ToolMode
{
	public override bool UseSnapGrid => true;
	[Property, ResourceSelect( Extension = "tdef", AllowPackages = true ), Title( "Thruster" )]
	public string Definition { get; set; } = "entities/thruster/basic.tdef";

	public override string Description => "#tool.hint.thrustertool.description";
	public override string PrimaryAction => "#tool.hint.thrustertool.place";
	public override string SecondaryAction => "#tool.hint.thrustertool.toggle_axis";

	Vector3 _axis = Vector3.Up;

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

		// Default: thrust away from surface normal. Toggle: thrust into surface (180° flip).
		var axisOffset = _axis == Vector3.Up ? new Angles( 90, 0, 0 ) : new Angles( -90, 0, 0 );

		var placementTrans = new Transform( pos.Position );
		placementTrans.Rotation = pos.Rotation * axisOffset;

		var thrusterDef = ResourceLibrary.Get<ThrusterDefinition>( Definition );
		if ( thrusterDef == null ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			Spawn( select, thrusterDef.Prefab, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( thrusterDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );

	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile thrusterPrefab, Transform tx )
	{
		if ( thrusterPrefab == null )
			return;

		var go = thrusterPrefab.GetScene().Clone();
		go.Tags.Add( "removable" );
		go.Tags.Add( "constraint" );
		go.WorldTransform = tx;

		if ( !point.GameObject.Tags.Contains( "world" ) )
		{
			var thruster = go.GetComponent<ThrusterEntity>();

			// attach it
			var joint = thruster.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.ToLocal( tx );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( go );

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
