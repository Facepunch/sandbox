/// <summary>
/// Recolours a physgun effect (the beam end, the grab ball and the sparks it spawns) in the holder's colour,
/// keeping each part's own brightness. Remembers the colours the prefab gave each part, so it can be applied every frame.
/// </summary>
public sealed class PhysgunEffectColor : Component
{
	readonly Dictionary<Component, Color> _original = new();

	/// <summary>
	/// Colour <paramref name="effect"/> and everything under it, back to the prefab's colours when <paramref name="color"/> is null
	/// </summary>
	public static void Apply( GameObject effect, Color? color )
	{
		effect.GetOrAddComponent<PhysgunEffectColor>().Recolor( color );
	}

	void Recolor( Color? color )
	{
		// parts spawned since the last frame, like the grab sparks, are picked up as they appear
		foreach ( var particles in GetComponentsInChildren<ParticleEffect>( true ) )
			particles.Tint = Physgun.Recolor( Original( particles, particles.Tint ), color );

		foreach ( var light in GetComponentsInChildren<PointLight>( true ) )
			light.LightColor = Physgun.Recolor( Original( light, light.LightColor ), color );

		foreach ( var model in GetComponentsInChildren<ModelRenderer>( true ) )
			model.Tint = Physgun.Recolor( Original( model, model.Tint ), color );
	}

	Color Original( Component component, Color current )
	{
		if ( _original.TryGetValue( component, out var original ) ) return original;

		_original[component] = current;
		return current;
	}
}
