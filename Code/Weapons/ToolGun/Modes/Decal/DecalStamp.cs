/// <summary>
/// A decal placed with the decal tool. It doesn't render itself, <see cref="DecalMerger"/> draws it,
/// baking overlapping stamps on the same surface into one texture so they're a single decal.
/// </summary>
public sealed class DecalStamp : Component
{
	/// <summary>
	/// How far the decal projects into the surface, the <see cref="Decal"/> default
	/// </summary>
	public const float Depth = 8f;

	[Property] public DecalDefinition Definition { get; set; }
	[Property] public Color Tint { get; set; } = Color.White;
	[Property] public float Scale { get; set; } = 1f;
	[Property] public float Rotation { get; set; }
	[Property] public float Attenuation { get; set; } = 1f;

	/// <summary>
	/// Paint order, higher layers draw over lower ones
	/// </summary>
	[Property] public uint Layer { get; set; }

	/// <summary>
	/// The decal's volume in its parent's space, the same box <see cref="Decal"/> projects. A unit cube maps onto it.
	/// </summary>
	public Transform Box
	{
		get
		{
			var tx = LocalTransform;
			tx.Scale *= new Vector3( Depth, Definition.Width * Scale, Definition.Height * Scale );
			tx.Rotation *= new Angles( 0, 0, Rotation );
			return tx;
		}
	}

	// nothing renders on a dedicated server, so nothing to draw it with
	protected override void OnEnabled()
	{
		if ( !Application.IsDedicatedServer ) DecalMerger.For( GameObject.Parent ).MarkDirty();
	}

	protected override void OnDisabled() => DecalMerger.Find( GameObject.Parent )?.MarkDirty();
}
