
[Icon( "🎈" )]
[ClassName( "balloon" )]
[Group( "Building" )]
public class Balloon : ToolMode
{
	// TODO: Choose model
	readonly Model model = Cloud.Model( "facepunch.balloonheart" );

	[Range( 0, 500 )]
	[Property, Sync]
	public float Length { get; set; } = 50.0f;

	[Range( -1, 1 )]
	[Property, Sync]
	public float Force { get; set; } = 0.2f;

	[Property, Sync]
	public bool Rigid { get; set; } = false;

	[Property, Sync]
	public Color Tint { get; set; } = Color.White;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();
		var placementTx = new Transform( pos.Position );

		if ( Input.Pressed( "attack1" ) )
		{
			Spawn( select, model, placementTx, true );
			ShootEffects( select );
		}
		else if ( Input.Pressed( "attack2" ) )
		{
			Spawn( select, model, placementTx, false );
			ShootEffects( select );
		}

		DebugOverlay.Model( model, transform: placementTx, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, Model model, Transform tx, bool withRope )
	{
		var go = new GameObject( false, "balloon" );
		go.Tags.Add( "removable" );
		go.WorldTransform = Rigid && withRope ? tx.WithPosition( tx.Position + Vector3.Up * Length ) : tx;

		var prop = go.AddComponent<Prop>();
		prop.Model = model;
		prop.Tint = Tint;

		if ( withRope )
		{
			var anchor = new GameObject( false, "anchor" );
			anchor.Parent = point.GameObject;
			anchor.LocalTransform = point.LocalTransform;

			var joint = go.AddComponent<SpringJoint>();
			joint.Body = anchor;
			joint.MinLength = Rigid ? Length : 0;
			joint.MaxLength = Length;
			joint.RestLength = Length;
			joint.Frequency = 0;
			joint.Damping = 0;
			joint.EnableCollision = true;

			var cleanup = go.AddComponent<ConstraintCleanup>();
			cleanup.Attachment = anchor;

			const float ropeWidth = 0.4f;
			var splineInterpolation = 0;
			if ( !Rigid )
			{
				var vertletRope = go.AddComponent<VerletRope>();
				vertletRope.Attachment = anchor;

				const int maxSegmentCount = 48;
				int segmentCount = Math.Min( maxSegmentCount, MathX.CeilToInt( Length / 16.0f ) );

				vertletRope.SegmentCount = segmentCount;
				vertletRope.Radius = ropeWidth;
				splineInterpolation = segmentCount > maxSegmentCount ? 8 : 4;
			}

			var lineRenderer = go.AddComponent<LineRenderer>();
			lineRenderer.Points = [go, anchor];
			lineRenderer.Width = ropeWidth;
			lineRenderer.Color = Color.White;
			lineRenderer.Lighting = true;
			lineRenderer.CastShadows = true;
			lineRenderer.SplineInterpolation = splineInterpolation;
			lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "materials/default/rope01.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
			lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;

			anchor.NetworkSpawn( true, null );
		}

		go.NetworkSpawn( true, null );

		var rb = go.GetComponent<Rigidbody>();
		if ( rb.IsValid() ) rb.GravityScale = -Force.Clamp( -1, 1 );

		var undo = Player.Undo.Create();
		undo.Name = "Balloon";
		undo.Add( go );
	}
}
