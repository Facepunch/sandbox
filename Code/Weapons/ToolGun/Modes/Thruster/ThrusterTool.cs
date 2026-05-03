﻿using Sandbox.UI;

[Hide]
[Title( "#tool.name.thruster" )]
[Icon( "🚀" )]
[ClassName( "thrustertool" )]
[Group( "#tool.group.building" )]
public class ThrusterTool : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, ResourceSelect( Extension = "tdef", AllowPackages = true ), Title( "Thruster" )]
	public string Definition { get; set; } = "entities/thruster/basic.tdef";

	public override string Description => "#tool.hint.thrustertool.description";

	Vector3 _axis = Vector3.Up;

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.thrustertool.place", OnPlace );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.thrustertool.place_no_weld", OnPlaceNoWeld );
		RegisterAction( ToolInput.Reload, () => "#tool.hint.thrustertool.toggle_axis", OnToggleAxis );
	}

	void OnToggleAxis()
	{
		_axis = _axis == Vector3.Right ? Vector3.Up : Vector3.Right;
	}

	void OnPlace()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var thrusterDef = ResourceLibrary.Get<ThrusterDefinition>( Definition );
		if ( thrusterDef == null ) return;

		var placementTrans = GetPlacementTransform( select );
		Spawn( select, thrusterDef.Prefab, placementTrans, false );
		ShootEffects( select );
	}

	void OnPlaceNoWeld()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var thrusterDef = ResourceLibrary.Get<ThrusterDefinition>( Definition );
		if ( thrusterDef == null ) return;

		var placementTrans = GetPlacementTransform( select );
		Spawn( select, thrusterDef.Prefab, placementTrans, true );
		ShootEffects( select );
	}

	Transform GetPlacementTransform( SelectionPoint select )
	{
		var pos = select.WorldTransform();
		var axisOffset = _axis == Vector3.Up ? new Angles( 90, 0, 0 ) : new Angles( -90, 0, 0 );

		var placementTrans = new Transform( pos.Position );
		placementTrans.Rotation = pos.Rotation * axisOffset;
		return placementTrans;
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var thrusterDef = ResourceLibrary.Get<ThrusterDefinition>( Definition );
		if ( thrusterDef == null ) return;

		var placementTrans = GetPlacementTransform( select );

		DebugOverlay.GameObject( thrusterDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile thrusterPrefab, Transform tx, bool noWeld )
	{
		if ( thrusterPrefab == null )
			return;

		var go = thrusterPrefab.GetScene().Clone();
		go.Tags.Add( "removable" );
		go.Tags.Add( "constraint" );
		go.WorldTransform = tx;

		if ( !noWeld && !point.GameObject.Tags.Contains( "world" ) )
		{
			var thruster = go.GetComponent<ThrusterEntity>();

			// attach it
			var joint = thruster.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.WithScale( 1 ).ToLocal( tx );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( go );

		go.NetworkSpawn( true, null );

		Track( go );

		// undo
		{
			var undo = Player.Undo.Create();
			undo.Name = "Thruster";
			undo.Icon = "🚀";
			undo.Add( go );
		}

		CheckContraptionStats( point.GameObject );
	}

}
