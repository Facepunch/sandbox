﻿[Hide]
[Title( "Hydraulic" )]
[Icon( "⚙️" )]
[ClassName( "HydraulicTool" )]
[Group( "Building" )]
public class HydraulicTool : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.hydraulictool.stage1" : "#tool.hint.hydraulictool.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.hydraulictool.finish" : "#tool.hint.hydraulictool.source";
	public override string ReloadAction => "#tool.hint.hydraulictool.remove";

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var cleanup in linked.GetComponentsInChildren<ConstraintCleanup>( true ) )
		{
			if ( linked != target && cleanup.Attachment?.Root != target ) continue;
			if ( cleanup.GameObject.GetComponentInChildren<HydraulicEntity>() is not null )
				yield return cleanup.GameObject;
		}
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		DebugOverlay.Line( point1.WorldPosition(), point2.WorldPosition(), Color.Red, 5.0f );

		if ( point1.GameObject == point2.GameObject )
			return;

		var line = point1.WorldPosition() - point2.WorldPosition();

		var go1 = new GameObject( false, "hydraulic_a" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.WorldRotation = Rotation.LookAt( -line );

		var go2 = new GameObject( false, "hydraulic_b" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.WorldRotation = Rotation.LookAt( -line );

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var len = (point1.WorldPosition() - point2.WorldPosition()).Length;

		SliderJoint joint = default;

		var jointGo = new GameObject( go1, true, "hydraulic" );

		// Joint
		{
			joint = jointGo.AddComponent<SliderJoint>();
			joint.Attachment = Joint.AttachmentMode.Auto;
			//	joint.AnchorBody = go1;
			joint.Body = go2;
			joint.MinLength = len;
			joint.MaxLength = len;
			joint.EnableCollision = true;
		}

		//
		// If it's ourself - we want to create the rope, but no joint between
		//
		var entity = jointGo.AddComponent<HydraulicEntity>();
		entity.Length = 0.5f;
		entity.MinLength = 5.0f;
		entity.MaxLength = len * 2.0f;
		entity.Joint = joint;

		var capsule = jointGo.AddComponent<CapsuleCollider>();
		var renderer = jointGo.AddComponent<SkinnedModelRenderer>();
		renderer.Model = Model.Load( "hydraulics/hydraulics_blockout.vmdl" );
		renderer.CreateBoneObjects = true;

		go2.NetworkSpawn( true, null );
		go1.NetworkSpawn( true, null );
		jointGo.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Hydraulic";
		undo.Add( go1 );
		undo.Add( go2 );
		undo.Add( jointGo );
	}

}

