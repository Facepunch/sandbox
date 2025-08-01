namespace Sandbox;

[Title("Line Renderer Light")]
public class LineRendererLight : Component, Component.ExecuteInEditor
{
    LineRenderer Renderer;
    SceneLight so;

    [Property] public float Brightness { get; set; } = 1.0f;
    
    public LineRendererLight()
    {
    }

    protected override void OnEnabled()
    {
        Renderer = Components.GetInAncestorsOrSelf<LineRenderer>();

        if ( Renderer is null )
        {
            Log.Warning( $"No line renderer component found for {this}" );
            return;
        }

        // Clean up any existing light
        if ( so.IsValid() )
            so.Delete();

        so = new SceneLight( Renderer.Scene.SceneWorld, Vector3.Zero, 100, Color.Red );
    }

	protected override void OnDisabled()
	{
		if ( !so.IsValid() ) return;
		so.Delete();
	}

    protected override void OnUpdate()
    {
        if (!so.IsValid() || Renderer == null) return;
        
        // Skip update if renderer has no points
        bool useVectorPoints = Renderer.UseVectorPoints;
        if ((useVectorPoints && (Renderer.VectorPoints == null || Renderer.VectorPoints.Count == 0)) ||
            (!useVectorPoints && (Renderer.Points == null || Renderer.Points.Count == 0)))
            return;
        
        Vector3 p1, p2;

        if (useVectorPoints)
        {
            p1 = Renderer.VectorPoints.First();
            p2 = Renderer.VectorPoints.Last();
        }
        else
        {
            p1 = Renderer.Points.First().WorldPosition;
            p2 = Renderer.Points.Last().WorldPosition;
        }

        float distance = p1.Distance(p2);
        
        
        so.Position = ( p1 + p2 ) / 2.0f;
        so.Radius = 256 + p1.Distance( p2 );
		so.LightColor = Renderer.Color.Evaluate( 0.5f ) * Brightness;
		so.ShapeSize = new Vector2( 2.0f, p1.Distance( p2 ) );
		so.Rotation = Rotation.LookAt( p2 - p1 );
		so.Shape = SceneLight.LightShape.Capsule;
    }
}