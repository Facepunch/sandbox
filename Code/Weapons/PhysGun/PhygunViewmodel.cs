public sealed class PhygunViewmodel : Component, Component.ExecuteInEditor
{
	[Property] public List<SpriteRenderer> TipSprites { get; set; }
	[Property] public ParticleEffect GlowEffect { get; set; }
	[Property] public ParticleEffect SparksEffect { get; set; }
	[Property] public Material TubeFxMaterial { get; set; }
	[Property] public Material BottleMaterial { get; set; }

	[Property] public bool BeamActive { get; set; }

	protected override void OnUpdate()
	{
		if ( GetComponentInParent<Physgun>() is Physgun physgun )
		{
			BeamActive = physgun.BeamActive;
		}

		UpdateGlowEffect();
		UpdateTipSprites();
		UpdateTubeFx();
		UpdateSparks();
		UpdateBottleGlow();
	}

	float _scroll;
	float _scrollSpeed;
	float _scrollSpeedVel;

	void UpdateTubeFx()
	{
		if ( TubeFxMaterial is null ) return;

		// ideally we'd scroll the self illum on its own - but that's not an option.
		// we have g_vSelfIllumScrollSpeed but we can't scale that speed up and down, because it's multiplied by time internally.

		_scrollSpeed = MathX.SmoothDamp( _scrollSpeed, BeamActive ? 2.0f : 0.2f, ref _scrollSpeedVel, BeamActive ? 0.5f : 2.5f, Time.Delta );
		_scroll += _scrollSpeed * Time.Delta;

		TubeFxMaterial.Set( "g_vSelfIllumOffset", new Vector2( _scroll % 1, 0 ) );
		TubeFxMaterial.Set( "g_flSelfIllumBrightness", 3 * (_scrollSpeed + 1.5) );
	}

	void UpdateBottleGlow()
	{
		if ( BottleMaterial is null ) return;

		float bounce = MathF.Sin( Time.Now * (BeamActive ? 45.0f : 3.0f) ) * 0.5f;


		BottleMaterial.Set( "g_flSelfIllumBrightness", (BeamActive ? 6.0f : 1.5f) + bounce );
	}

	void UpdateTipSprites()
	{
		var mul = BeamActive ? 1.0f : 0.6f;

		foreach ( var sprite in TipSprites )
		{
			sprite.Enabled = true;
			sprite.Color = sprite.Color.WithAlpha( mul * Random.Shared.Float( 0.4f, 0.9f ) );
			sprite.Size = Random.Shared.Float( 6, 7 ) * mul;
		}
	}

	void UpdateGlowEffect()
	{
		if ( GlowEffect is null ) return;

		GlowEffect.Alpha = BeamActive ? 1.0f : 0.2f;
	}


	bool _wasActive;

	void UpdateSparks()
	{
		if ( SparksEffect is null ) return;

		if ( BeamActive == _wasActive ) return;

		_wasActive = BeamActive;

		int count = BeamActive ? 20 : 3;

		for ( int i = 0; i < count; i++ )
		{
			var p = SparksEffect.Emit( SparksEffect.WorldPosition, i / (float)count );
			p.Velocity = Vector3.Random * 100.0f + SparksEffect.WorldTransform.Forward * 30.0f;
		}
	}
}
