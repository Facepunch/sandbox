public abstract partial class ToolMode
{
	/// <summary>
	/// Radius within which <see cref="IToolModePreview"/> components receive their per-frame callback.
	/// </summary>
	protected virtual float PreviewRadius => 512f;

	/// <summary>
	/// Calls <see cref="IToolModePreview.OnToolModePreview"/> on every component within <see cref="PreviewRadius"/>.
	/// Invoked automatically each frame from the base <see cref="OnControl"/>.
	/// </summary>
	protected void UpdateNearbyPreviews()
	{
		var eyePos = Player.EyeTransform.Position;
		var radiusSq = PreviewRadius * PreviewRadius;

		foreach ( var preview in Scene.GetAllComponents<IToolModePreview>() )
		{
			var go = (preview as Component)?.GameObject;
			if ( !go.IsValid() ) continue;
			if ( (go.WorldPosition - eyePos).LengthSquared > radiusSq ) continue;

			preview.OnToolModePreview();
		}
	}
}
