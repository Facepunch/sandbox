
/// <summary>
/// Shared geometry for two-point length-changing constraints (Hydraulic, Muscle, Winch):
/// a shaft between two capped anchors, driven by a <see cref="SliderJoint"/>.
/// </summary>
public abstract class BaseLengthConstraintTool : BaseConstraintToolMode
{
	protected abstract string CapModelA { get; }
	protected abstract string CapModelB { get; }
	protected abstract string ShaftMaterial { get; }
	protected abstract string UndoName { get; }

	protected abstract LengthConstraintEntity.ConstraintKind Kind { get; }

	protected virtual float ShaftWidth => 2f;
	protected virtual float StartLength => 0.5f;
	protected virtual float MinLength => 5.0f;
	protected virtual float MaxLengthMultiplier => 2.0f;
	protected virtual float PushSpeed => 0.25f;
	protected virtual float PullSpeed => 0.25f;
	protected virtual bool AnimatedByDefault => false;

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var cleanup in linked.GetComponentsInChildren<ConstraintCleanup>( true ) )
		{
			if ( linked != target && cleanup.Attachment?.Root != target ) continue;
			if ( cleanup.GameObject.GetComponentInChildren<LengthConstraintEntity>() is not null )
				yield return cleanup.GameObject;
		}
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( point1.GameObject == point2.GameObject )
			return;

		var line = point1.WorldPosition() - point2.WorldPosition();
		var baseName = UndoName.ToLowerInvariant();

		var go1 = new GameObject( false, $"{baseName}_a" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.WorldRotation = Rotation.LookAt( -line );
		go1.Tags.Add( "constraint" );

		var go2 = new GameObject( false, $"{baseName}_b" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.WorldRotation = Rotation.LookAt( -line );
		go2.Tags.Add( "constraint" );

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var len = (point1.WorldPosition() - point2.WorldPosition()).Length;

		// End caps
		var capA = new GameObject( go1, true, $"{baseName}_cap_a" );
		capA.LocalPosition = Vector3.Zero;
		capA.WorldRotation = Rotation.LookAt( line ) * Rotation.FromPitch( -90f );
		capA.AddComponent<ModelRenderer>().Model = Model.Load( CapModelA );

		var capB = new GameObject( go2, true, $"{baseName}_cap_b" );
		capB.LocalPosition = Vector3.Zero;
		capB.WorldRotation = Rotation.LookAt( -line ) * Rotation.FromPitch( -90f );
		capB.AddComponent<ModelRenderer>().Model = Model.Load( CapModelB );

		// Shaft, using line renderer
		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;
		lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( ShaftMaterial ), WorldSpace = true, UnitsPerTexture = 32 };
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;
		lineRenderer.Width = ShaftWidth;
		lineRenderer.Color = Color.White;

		SliderJoint joint;

		var jointGo = new GameObject( go1, true, baseName );

		// Joint
		{
			joint = jointGo.AddComponent<SliderJoint>();
			joint.Attachment = Sandbox.Joint.AttachmentMode.Auto;
			joint.Body = go2;
			joint.MinLength = len;
			joint.MaxLength = len;
			joint.EnableCollision = true;
		}

		var entity = jointGo.AddComponent<LengthConstraintEntity>();
		entity.Kind = Kind;
		entity.Length = StartLength;
		entity.MinLength = MinLength;
		entity.MaxLength = len * MaxLengthMultiplier;
		entity.PushSpeed = PushSpeed;
		entity.PullSpeed = PullSpeed;
		entity.Animated = AnimatedByDefault;
		entity.Joint = joint;

		jointGo.AddComponent<CapsuleCollider>();

		// The joint is the part with a collider, so it's what the remover tool hits. Tag it removable
		// and tie it to the anchors, so removing the shaft takes the caps and anchors with it (and the
		// joint goes when its anchor does).
		jointGo.Tags.Add( "removable" );
		jointGo.Tags.Add( "constraint" );
		jointGo.AddComponent<ConstraintCleanup>().Attachment = go1;

		go2.NetworkSpawn( true, null );
		go1.NetworkSpawn( true, null );
		jointGo.NetworkSpawn( true, null );

		Track( go1, go2, jointGo );

		var undo = Player.Undo.Create();
		undo.Name = UndoName;
		undo.Add( go1 );
		undo.Add( go2 );
		undo.Add( jointGo );
	}

}
