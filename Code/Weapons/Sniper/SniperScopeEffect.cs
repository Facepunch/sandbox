using Sandbox.Rendering;

public sealed class SniperScopeEffect : BasePostProcess<SniperScopeEffect>
{
	public float BlurInput { get; set; }

	private float _smoothedBlur;
	private static Material _cachedMaterial;

	public override void Render()
	{
		_smoothedBlur = _smoothedBlur.LerpTo( BlurInput, Time.Delta * 8f );
		var blurAmount = (1.0f - _smoothedBlur).Clamp( 0.1f, 1f );

		Attributes.Set( "BlurAmount", blurAmount );
		Attributes.Set( "Offset", Vector2.Zero );

		_cachedMaterial ??= Material.FromShader( "shaders/postprocess/sniper_scope.shader" );
		var blit = BlitMode.WithBackbuffer( _cachedMaterial, Stage.AfterPostProcess, 200, false );
		Blit( blit, "SniperScope" );
	}
}
