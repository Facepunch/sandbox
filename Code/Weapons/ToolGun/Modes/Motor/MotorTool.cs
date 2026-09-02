
[Hide]
[Title( "#tool.name.motor" )]
[Icon( "🔄" )]
[ClassName( "MotorTool" )]
[Group( "#tool.group.constraints" )]
public sealed class MotorTool : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.motortool.stage1" : "#tool.hint.motortool.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.motortool.finish" : "#tool.hint.motortool.source";
	public override string ReloadAction => "#tool.hint.motortool.remove";

	/// <summary>
	/// The spin axis is the surface normal where you make the first selection - this flips it
	/// 180 degrees, which is only useful in combination with <see cref="Reversed"/> and a limit range.
	/// </summary>
	[Property, Sync]
	public bool FlipAxis { get; set; } = false;

	[Property, Sync]
	public bool Reversed { get; set; } = false;

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var joint in linked.GetComponentsInChildren<HingeJoint>( true ) )
			if ( joint.GetComponent<MotorEntity>() is not null && (linked == target || joint.Body?.Root == target) )
				yield return joint.GameObject;
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( point1.GameObject == point2.GameObject )
			return;

		// The hinge spins around its anchor's local Up - build a rotation whose Up matches the
		// normal at the first click (i.e. point at the surface you want to spin around).
		var normal = point1.WorldTransform().Rotation.Forward * (FlipAxis ? -1f : 1f);
		var reference = MathF.Abs( normal.Dot( Vector3.Up ) ) > 0.99f ? Vector3.Forward : Vector3.Up;
		var axisForward = Vector3.Cross( reference, normal ).Normal;
		var axisRotation = Rotation.LookAt( axisForward, normal );

		var go1 = new GameObject( false, "motor_a" );
		go1.Parent = point1.GameObject;
		go1.WorldPosition = point1.WorldPosition();
		go1.WorldRotation = axisRotation;
		go1.Tags.Add( "constraint" );

		var go2 = new GameObject( false, "motor_b" );
		go2.Parent = point2.GameObject;
		go2.WorldTransform = go1.WorldTransform;
		go2.Tags.Add( "constraint" );

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var joint = go1.AddComponent<HingeJoint>();
		joint.Attachment = Sandbox.Joint.AttachmentMode.Auto;
		joint.Body = go2;
		joint.EnableCollision = false;

		var entity = go1.AddComponent<MotorEntity>();
		entity.Reversed = Reversed;
		entity.Joint = joint;

		go2.NetworkSpawn( true, null );
		go1.NetworkSpawn( true, null );

		Track( go1, go2 );

		var undo = Player.Undo.Create();
		undo.Name = "Motor";
		undo.Add( go1 );
		undo.Add( go2 );

		CheckContraptionStats( point1.GameObject );
	}
}
