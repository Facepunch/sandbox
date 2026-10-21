public sealed class PlacementPreview
{
	readonly List<ModelRenderer> _faded = [];
	readonly List<ModelRenderer> _fading = [];
	Material _material;

	/// <summary>
	/// Preview <paramref name="source"/> and everything linked to it moved so that <paramref name="source"/> sits at <paramref name="placement"/>.
	/// </summary>
	public void Draw( Scene scene, GameObject source, Transform placement )
	{
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( source );

		Draw( scene, source, placement, builder.Objects );
	}

	/// <summary>
	/// Preview <paramref name="objects"/> moved along with <paramref name="source"/> to <paramref name="placement"/>.
	/// </summary>
	public void Draw( Scene scene, GameObject source, Transform placement, IEnumerable<GameObject> objects )
	{
		_material ??= Material.Load( "materials/effects/duplicator_override.vmat" );

		foreach ( var obj in objects )
		{
			var transform = obj == source
				? placement
				: placement.ToWorld( source.WorldTransform.ToLocal( obj.WorldTransform ) );

			scene.DebugOverlay.GameObject( obj, transform: transform, castShadows: false, materialOveride: _material );

			_fading.AddRange( obj.GetComponentsInChildren<ModelRenderer>() );
		}
	}

	/// <summary>
	/// Fade what was drawn this frame to a pulsing half opacity, and put back anything that wasn't.
	/// Only the scene objects change, the renderers' tints are left alone so nothing faded gets saved or sent.
	/// </summary>
	public void EndFrame()
	{
		foreach ( var renderer in _faded )
		{
			if ( renderer.IsValid() && !_fading.Contains( renderer ) && renderer.SceneObject.IsValid() )
				renderer.SceneObject.ColorTint = renderer.Tint;
		}

		_faded.Clear();

		var opacity = 0.5f + 0.15f * MathF.Sin( Time.Now * MathF.PI * 3.0f );

		foreach ( var renderer in _fading )
		{
			if ( !renderer.IsValid() || !renderer.SceneObject.IsValid() ) continue;

			renderer.SceneObject.ColorTint = renderer.Tint.WithAlpha( renderer.Tint.a * opacity );
			_faded.Add( renderer );
		}

		_fading.Clear();
	}

	/// <summary>
	/// Stop previewing and put every faded object back.
	/// </summary>
	public void Clear()
	{
		_fading.Clear();
		EndFrame();
	}
}
