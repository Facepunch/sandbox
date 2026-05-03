﻿
[Icon( "➖" )]
[Title( "#tool.name.slider" )]
[ClassName( "slider" )]
[Group( "#tool.group.constraints" )]
public class Slider : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.slider.stage1" : "#tool.hint.slider.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.slider.finish" : "#tool.hint.slider.source";
	public override string SecondaryAction => Stage == 1 ? "#tool.hint.slider.secondary.stage1" : "#tool.hint.slider.secondary";
	public override string ReloadAction => "#tool.hint.slider.remove";

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var joint in linked.GetComponentsInChildren<SliderJoint>( true ) )
			if ( linked == target || joint.Body?.Root == target )
				yield return joint.GameObject;
	}

	protected override SelectionPoint? GetSecondaryPoint( SelectionPoint select )
	{
		return TraceFromRay( select.WorldTransform().ForwardRay, 4096, select.GameObject );
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( point1.GameObject == point2.GameObject )
			return;

		var axis = Rotation.LookAt( Vector3.Direction( point1.WorldPosition(), point2.WorldPosition() ) );

		var go1 = new GameObject( false, "slider" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.WorldRotation = axis;

		var go2 = new GameObject( false, "slider" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.WorldRotation = axis;

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var len = point1.WorldPosition().Distance( point2.WorldPosition() );

		var joint = go1.AddComponent<SliderJoint>();
		joint.Body = go2;
		joint.MinLength = 0;
		joint.MaxLength = len;
		joint.EnableCollision = true;

		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Width = 0.5f;
		lineRenderer.Color = Color.Black;
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		Track( go1, go2 );

		var undo = Player.Undo.Create();
		undo.Name = "Slider";
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
