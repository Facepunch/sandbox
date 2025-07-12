
[Icon( "🐍" )]
[ClassName( "rope" )]
public class Rope : ToolMode
{
	[Range( -500, 500 )]
	[Property]
	public float Slack { get; set; } = 0.0f;

	[Property]
	public bool Rigid { get; set; } = false;

	SelectionPoint _point1;
	SelectionPoint _point2;
	int stage = 0;

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Pressed( "attack1" ) )
		{
			var select = TraceSelect();

			if ( !select.IsValid() )
				return;

			if ( stage == 0 )
			{
				_point1 = select;
				stage++;
				ShootEffects( select );
				return;
			}

			if ( stage == 1 )
			{
				_point2 = select;

				CreateRope( _point1, _point2 );
				ShootEffects( select );
			}

			stage = 0;

		}
	}

	[Rpc.Host]
	private void CreateRope( SelectionPoint point1, SelectionPoint point2 )
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

		var fixedJoint = go1.AddComponent<SpringJoint>();
		fixedJoint.Body = go2;
		fixedJoint.MinLength = Rigid ? len : 0;
		fixedJoint.MaxLength = len;
		fixedJoint.RestLength = len;
		fixedJoint.Frequency = 0;
		fixedJoint.Damping = 0;
		fixedJoint.EnableCollision = true;

		if ( !Rigid )
		{
			var vertletRope = go1.AddComponent<VerletRope>();
			vertletRope.Attachment = go2;
			vertletRope.SegmentCount = Math.Max( 2, MathX.CeilToInt( len / 16.0f ) );
			vertletRope.SegmentLength = (len / vertletRope.SegmentCount);
			vertletRope.ConstraintIterations = 2;
		}

		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Width = 0.5f;
		lineRenderer.Color = Color.White;
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
