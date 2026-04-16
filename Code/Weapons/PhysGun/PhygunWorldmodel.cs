public sealed class PhygunWorldmodel : Component
{
	[Property] public ParticleEffect GlowEffect { get; set; }
	[Property] public PointLight GlowLight { get; set; }

	[Property] public Color GravTint { get; set; } = new Color( 1f, 0.8f, 0f );
	[Property] public Color PhysTint { get; set; } = new Color( 0f, 0.68333f, 1f );

	float _tintFrac;

	protected override void OnUpdate()
	{
		var physgun = GameObject.Root.Components.Get<Physgun>( FindMode.EverythingInSelfAndDescendants );
		var pullActive = physgun?.PullActive ?? false;

		_tintFrac = MathX.Approach( _tintFrac, pullActive ? 1 : 0, Time.Delta * 5 );

		var tint = _tintFrac <= 0.5f
			? Color.Lerp( PhysTint, Color.White, _tintFrac * 2 )
			: Color.Lerp( Color.White, GravTint, (_tintFrac - 0.5f) * 2 );

		if ( GlowEffect is not null )
			GlowEffect.Tint = tint;

		if ( GlowLight is not null )
			GlowLight.LightColor = tint;
	}
}
