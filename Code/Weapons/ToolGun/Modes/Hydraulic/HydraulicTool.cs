[Hide]
[Title( "Hydraulic" )]
[Icon( "⚙️" )]
[ClassName( "HydraulicTool" )]
[Group( "Building" )]
public class HydraulicTool : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.hydraulictool.stage1" : "#tool.hint.hydraulictool.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.hydraulictool.finish" : "#tool.hint.hydraulictool.source";
	public override string ReloadAction => "#tool.hint.hydraulictool.remove";

	[Property, Sync]
	public bool BallJoints { get; set; } = false;

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

		if ( BallJoints )
		{
			CreateBallJointHydraulic( point1, point2 );
			return;
		}

		var line = point1.WorldPosition() - point2.WorldPosition();

		var go1 = new GameObject( false, "hydraulic_a" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.WorldRotation = Rotation.LookAt( -line );
		go1.Tags.Add( "constraint" );

		var go2 = new GameObject( false, "hydraulic_b" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.WorldRotation = Rotation.LookAt( -line );
		go2.Tags.Add( "constraint" );

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var len = (point1.WorldPosition() - point2.WorldPosition()).Length;

		// End caps
		var capA = new GameObject( go1, true, "hydraulic_cap_a" );
		capA.LocalPosition = Vector3.Zero;
		capA.WorldRotation = Rotation.LookAt( line ) * Rotation.FromPitch( -90f );
		capA.AddComponent<ModelRenderer>().Model = Model.Load( "hydraulics/tool_hydraulic.vmdl" );

		var capB = new GameObject( go2, true, "hydraulic_cap_b" );
		capB.LocalPosition = Vector3.Zero;
		capB.WorldRotation = Rotation.LookAt( -line ) * Rotation.FromPitch( -90f );
		capB.AddComponent<ModelRenderer>().Model = Model.Load( "hydraulics/tool_hydraulic.vmdl" );

		// Shaft, using line renderer
		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;
		lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "hydraulics/metal_tile_line.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;
		lineRenderer.Width = 2f;
		lineRenderer.Color = Color.White;

		SliderJoint joint = default;

		var jointGo = new GameObject( go1, true, "hydraulic" );

		// Joint
		{
			joint = jointGo.AddComponent<SliderJoint>();
			joint.Attachment = Joint.AttachmentMode.Auto;
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

		go2.NetworkSpawn( true, null );
		go1.NetworkSpawn( true, null );
		jointGo.NetworkSpawn( true, null );

		Track( go1, go2, jointGo );

		var undo = Player.Undo.Create();
		undo.Name = "Hydraulic";
		undo.Add( go1 );
		undo.Add( go2 );
		undo.Add( jointGo );
	}

	private void CreateBallJointHydraulic( SelectionPoint point1, SelectionPoint point2 )
	{
		var p1 = point1.WorldPosition();
		var p2 = point2.WorldPosition();
		var len = p1.Distance( p2 );

		var dir = (p2 - p1).Normal;
		var up = MathF.Abs( Vector3.Dot( dir, Vector3.Up ) ) > 0.99f ? Vector3.Forward : Vector3.Up;
		var axis = Rotation.LookAt( dir );

		var surfaceRotA = point1.WorldTransform().Rotation;
		var surfaceRotB = point2.WorldTransform().Rotation;

		// Visual anchors — identity rotation for LineRenderer
		var goA = new GameObject( false, "bs_hydraulic_a" );
		goA.Parent = point1.GameObject;
		goA.LocalTransform = point1.LocalTransform;
		goA.LocalRotation = Rotation.Identity;
		goA.Tags.Add( "constraint" );

		var goB = new GameObject( false, "bs_hydraulic_b" );
		goB.Parent = point2.GameObject;
		goB.LocalTransform = point2.LocalTransform;
		goB.LocalRotation = Rotation.Identity;
		goB.Tags.Add( "constraint" );

		var cleanup = goA.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = goB;

		// Base mount visuals — surface normal aligned
		var baseVisA = new GameObject( goA, true, "hydraulic_base_a" );
		baseVisA.LocalPosition = Vector3.Zero;
		baseVisA.WorldRotation = surfaceRotA * Rotation.FromPitch( 90f );
		baseVisA.AddComponent<ModelRenderer>().Model = Model.Load( "hydraulics/tool_suspension_base.vmdl" );

		var baseVisB = new GameObject( goB, true, "hydraulic_base_b" );
		baseVisB.LocalPosition = Vector3.Zero;
		baseVisB.WorldRotation = surfaceRotB * Rotation.FromPitch( 90f );
		baseVisB.AddComponent<ModelRenderer>().Model = Model.Load( "hydraulics/tool_suspension_base.vmdl" );

		// Ball joint visuals
		var ballVisA = new GameObject( goA, true, "hydraulic_ball_a" );
		ballVisA.WorldPosition = goA.WorldPosition + surfaceRotA.Forward * 4.644f;
		ballVisA.WorldRotation = Rotation.LookAt( -dir, up ) * Rotation.FromPitch( -90f );
		var skinA = ballVisA.AddComponent<SkinnedModelRenderer>();
		skinA.Model = Model.Load( "hydraulics/tool_balljoint.vmdl" );
		skinA.CreateBoneObjects = true;

		var ballVisB = new GameObject( goB, true, "hydraulic_ball_b" );
		ballVisB.WorldPosition = goB.WorldPosition + surfaceRotB.Forward * 4.644f;
		ballVisB.WorldRotation = Rotation.LookAt( dir, up ) * Rotation.FromPitch( -90f );
		var skinB = ballVisB.AddComponent<SkinnedModelRenderer>();
		skinB.Model = Model.Load( "hydraulics/tool_balljoint.vmdl" );
		skinB.CreateBoneObjects = true;

		// Shaft
		var lineRenderer = goA.AddComponent<LineRenderer>();
		lineRenderer.Points = [ballVisA, ballVisB];
		lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;
		lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "hydraulics/metal_tile_line.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;
		lineRenderer.Width = 1.5f;
		lineRenderer.Color = Color.White;

		var aligner = goA.AddComponent<BallSocketPair>();
		aligner.BallModelA = skinA;
		aligner.BallModelB = skinB;
		aligner.ShaftRenderer = lineRenderer;

		// Ball socket constraint
		var ballTarget = new GameObject( point2.GameObject, false, "bs_target" );
		ballTarget.LocalTransform = point2.LocalTransform;

		var ballAnchor = new GameObject( point1.GameObject, false, "bs_anchor" );
		ballAnchor.WorldTransform = ballTarget.WorldTransform;

		var ballJoint = ballAnchor.AddComponent<BallJoint>();
		ballJoint.Body = ballTarget;
		ballJoint.Friction = 0.0f;
		ballJoint.EnableCollision = false;

		// Slider joint
		var sliderA = new GameObject( false, "hydraulic_slider_a" );
		sliderA.Parent = point1.GameObject;
		sliderA.LocalTransform = point1.LocalTransform;
		sliderA.WorldRotation = axis;

		var sliderB = new GameObject( false, "hydraulic_slider_b" );
		sliderB.Parent = point2.GameObject;
		sliderB.LocalTransform = point2.LocalTransform;
		sliderB.WorldRotation = axis;

		var slider = sliderA.AddComponent<SliderJoint>();
		slider.Body = sliderB;
		slider.MinLength = len;
		slider.MaxLength = len;
		slider.EnableCollision = true;

		var entity = sliderA.AddComponent<HydraulicEntity>();
		entity.Length = 0.5f;
		entity.MinLength = 5.0f;
		entity.MaxLength = len * 2.0f;
		entity.Joint = slider;

		sliderA.AddComponent<CapsuleCollider>();

		// TODO: my lord
		goB.NetworkSpawn( true, null );
		goA.NetworkSpawn( true, null );
		ballTarget.NetworkSpawn( true, null );
		ballAnchor.NetworkSpawn( true, null );
		sliderB.NetworkSpawn( true, null );
		sliderA.NetworkSpawn( true, null );

		Track( goA, goB, ballAnchor, ballTarget, sliderA, sliderB );

		var undo = Player.Undo.Create();
		undo.Name = "Hydraulic (Ball Joints)";
		undo.Add( goA, goB, ballAnchor, ballTarget, sliderA, sliderB );
	}
}

