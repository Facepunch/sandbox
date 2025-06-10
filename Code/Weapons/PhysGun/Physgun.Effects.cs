using Sandbox.Utility;

public partial class Physgun : BaseCarryable
{
	[Property] public LineRenderer BeamRenderer { get; set; }

	Vector3.SpringDamped middleSpring = new Vector3.SpringDamped( 0, 0, 0.1f );

	void UpdateBeam( Transform source, Vector3 end, Vector3 endNormal )
	{
		if ( !BeamRenderer.IsValid() ) return;

		bool justEnabled = !BeamRenderer.Enabled;

		if ( BeamRenderer.VectorPoints.Count != 4 )
			BeamRenderer.VectorPoints = new List<Vector3>( [0, 0, 0, 0] );

		var distance = source.Position.Distance( end );

		BeamRenderer.VectorPoints[0] = source.Position;


		var targetMiddle = source.Position + source.Forward * distance * 0.33f + source.Up * 1.0f;
		targetMiddle = targetMiddle + Noise.FbmVector( 2, Time.Now * 400.0f, Time.Now * 100.0f ) * 1.0f;

		BeamRenderer.VectorPoints[1] = middleSpring.Current;
		middleSpring.Target = targetMiddle;
		middleSpring.Update( Time.Delta );


		if ( justEnabled )
		{
			BeamRenderer.Enabled = true;
			BeamRenderer.VectorPoints[1] = targetMiddle;
			middleSpring = new Vector3.SpringDamped( targetMiddle, targetMiddle, 0.2f, 4, 0.2f );
		}

		BeamRenderer.VectorPoints[2] = Vector3.Lerp( (end + endNormal * 10), BeamRenderer.VectorPoints[1], 0.3f + MathF.Sin( Time.Now * 10.0f ) * 0.2f );
		BeamRenderer.VectorPoints[3] = end;
	}

	void CloseBeam()
	{
		BeamRenderer.Enabled = false;
	}

}
