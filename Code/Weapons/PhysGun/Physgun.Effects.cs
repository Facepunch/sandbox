using Sandbox.Utility;

public partial class Physgun : BaseCarryable
{
	[Property] public LineRenderer BeamRenderer { get; set; }

	Vector3.SpringDamped middleSpring = new Vector3.SpringDamped( 0, 0, 0.1f );

	void UpdateBeam( Transform source, Vector3 end, Vector3 endNormal )
	{
		if ( !BeamRenderer.IsValid() ) return;

		// obj
		if ( _state.GameObject.IsValid() )
		{
			BeamHighlight.Enabled = true;
			BeamHighlight.OverrideTargets = true;
			BeamHighlight.Targets.Clear();
			BeamHighlight.Targets.AddRange( _state.GameObject.GetComponents<Renderer>() );
			BeamHighlight.Width = 0.1f + Noise.Fbm( 3, Time.Now * 100.0f ) * 0.1f;
			BeamHighlight.Color = Color.Lerp( Color.Cyan, Color.White, Noise.Fbm( 3, Time.Now * 40.0f ) * 0.5f ) * 200.0f;
		}

		bool justEnabled = !BeamRenderer.GameObject.Enabled;

		if ( BeamRenderer.VectorPoints.Count != 4 )
			BeamRenderer.VectorPoints = new List<Vector3>( [0, 0, 0, 0] );

		var distance = source.Position.Distance( end );

		BeamRenderer.VectorPoints[0] = source.Position;


		var targetMiddle = source.Position + source.Forward * distance * 0.33f;
		targetMiddle = targetMiddle + Noise.FbmVector( 2, Time.Now * 400.0f, Time.Now * 100.0f ) * 1.0f;

		BeamRenderer.VectorPoints[1] = middleSpring.Current;
		middleSpring.Target = targetMiddle;
		middleSpring.Update( Time.Delta );

		BeamRenderer.VectorPoints[2] = Vector3.Lerp( (end + endNormal * 10), BeamRenderer.VectorPoints[1], 0.3f + MathF.Sin( Time.Now * 10.0f ) * 0.2f );
		BeamRenderer.VectorPoints[3] = end;

		if ( justEnabled )
		{
			BeamRenderer.GameObject.Enabled = true;
			BeamRenderer.VectorPoints[1] = targetMiddle;
			middleSpring = new Vector3.SpringDamped( targetMiddle, targetMiddle, 0.2f, 4, 0.2f );
		}


	}

	void CloseBeam()
	{
		if ( _stateHovered.GameObject.IsValid() )
		{
			BeamHighlight.Enabled = true;
			BeamHighlight.OverrideTargets = true;
			BeamHighlight.Targets.Clear();
			BeamHighlight.Targets.AddRange( _stateHovered.GameObject.GetComponents<Renderer>() );
			BeamHighlight.Width = 0.2f;
			BeamHighlight.Color = new Color( 0.5f, 1, 1, 0.3f );
		}
		else
		{
			BeamHighlight.Enabled = false;
		}

		if ( !BeamRenderer.IsValid() ) return;

		BeamRenderer.GameObject.Enabled = false;
	}

}
