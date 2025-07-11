
[Icon( "➖" )]
[ClassName( "slider" )]
public class Slider : ToolMode
{
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

				CreateSlider( _point1, _point2 );
				ShootEffects( select );
			}

			stage = 0;
		}
	}

	[Rpc.Host]
	private void CreateSlider( SelectionPoint point1, SelectionPoint point2 )
	{
		var axis = Rotation.LookAt( Vector3.Direction( point1.WorldPosition(), point2.WorldPosition() ) );

		var go1 = new GameObject( false, "slider" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.WorldRotation = axis;

		var go2 = new GameObject( false, "slider" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.WorldRotation = axis;

		var len = point1.WorldPosition().Distance( point2.WorldPosition() );

		var fixedJoint = go1.AddComponent<SliderJoint>();
		fixedJoint.Body = go2;
		fixedJoint.MinLength = 0;
		fixedJoint.MaxLength = len;
		fixedJoint.EnableCollision = false;

		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Width = 0.5f;
		lineRenderer.Color = Color.Black;
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
