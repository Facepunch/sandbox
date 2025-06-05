
/// <summary>
/// An extension to the Surface GameResource to add our own effects to surfaces.
/// </summary>
[GameResource( "Surface Extension", "simpact", "Surface Impacts", Icon = "💥", IconBgColor = "#111" )]
public class SurfaceImpacts : ResourceExtension<Surface, SurfaceImpacts>
{
	public GameObject BulletImpact { get; set; }
	public GameObject BulletDecal { get; set; }
}

