
[Icon( "🐍" )]
[ClassName( "rope" )]
[Group( "Constraints" )]
public class Rope : BaseConstraintToolMode
{
	[Range( -500, 500 )]
	[Property, Sync]
	public float Slack { get; set; } = 0.0f;

	[Property, Sync]
	public bool Rigid { get; set; } = false;

	[Range( 0.5f, 5f ), Step( 0.5f )]
	[Property]
	public float Radius { get; set; } = 1f;
	
	public override bool CanConstraintToSelf => true;

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go1 = new GameObject( false, "rope" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.LocalRotation = Rotation.Identity;

		var go2 = new GameObject( false, "rope" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.LocalRotation = Rotation.Identity;

		var len = point1.WorldPosition().Distance( point2.WorldPosition() );
		len = MathF.Max( 1.0f, len + Slack );

		//
		// If it's ourself - we want to create the rope, but no joint between
		//
		if ( point1.GameObject != point2.GameObject )
		{
			var joint = go1.AddComponent<SpringJoint>();
			joint.Body = go2;
			joint.MinLength = Rigid ? len : 0;
			joint.MaxLength = len;
			joint.RestLength = len;
			joint.Frequency = 0;
			joint.Damping = 0;
			joint.EnableCollision = true;
		}

		var splineInterpolation = 0;
		if ( !Rigid )
		{
			var vertletRope = go1.AddComponent<VerletRope>();
			vertletRope.Attachment = go2;

			const int maxSegmentCount = 48;
			// Maximum segment count, so long ropes don't exceed computation limits
			int segmentCount = Math.Min( maxSegmentCount, MathX.CeilToInt( len / 16f ) );

			vertletRope.SegmentCount = segmentCount;
			vertletRope.SegmentLength = len / segmentCount;
			vertletRope.ConstraintIterations = MathX.CeilToInt( segmentCount * 1.3f );
			vertletRope.Radius = Radius;
			splineInterpolation = segmentCount > maxSegmentCount ? 8 : 4;
		}

		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Width = Radius;
		lineRenderer.Color = Color.White;
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;
		lineRenderer.SplineInterpolation = splineInterpolation;
		lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "materials/default/rope01.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
		lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;

		go2.NetworkSpawn( true, null );
		go1.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
