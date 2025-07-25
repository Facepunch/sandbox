
[Icon( "🌀" )]
[ClassName( "elastic" )]
[Group( "Constraints" )]
public class Elastic : BaseConstraintToolMode
{
	[Range( 0, 15 )]
	[Property]
	public float Frequency { get; set; } = 2.0f;

	[Range( 0, 4 )]
	[Property]
	public float Damping { get; set; } = 0.1f;

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go1 = new GameObject( false, "elastic" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.LocalRotation = Rotation.Identity;

		var go2 = new GameObject( false, "elastic" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.LocalRotation = Rotation.Identity;

		var len = point1.WorldPosition().Distance( point2.WorldPosition() );

		if ( point1.GameObject != point2.GameObject )
		{
			var joint = go1.AddComponent<SpringJoint>();
			joint.Body = go2;
			joint.MinLength = 0;
			joint.MaxLength = float.MaxValue;
			joint.RestLength = len;
			joint.Frequency = Frequency;
			joint.Damping = Damping;
			joint.EnableCollision = true;
		}

		var vertletRope = go1.AddComponent<VerletRope>();
		vertletRope.Attachment = go2;
		vertletRope.SegmentCount = MathX.CeilToInt( len / 16.0f );
		vertletRope.SegmentLength = (len / vertletRope.SegmentCount);
		vertletRope.ConstraintIterations = 2;

		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Width = 0.5f;
		lineRenderer.Color = Color.Orange;
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
