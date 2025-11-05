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
			BeamActive = physgun.BeamRenderer?.Active ?? false;
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

		_scrollSpeed = MathX.SmoothDamp( _scrollSpeed, BeamActive ? 3.0f : 0.05f, ref _scrollSpeedVel, BeamActive ? 0.5f : 2.5f, Time.Delta );
		_scroll += _scrollSpeed * Time.Delta;

		TubeFxMaterial.Set( "g_vTexCoordOffset", new Vector2( _scroll % 1.0f, 0 ) );
		TubeFxMaterial.Set( "g_flSelfIllumBrightness", BeamActive ? 8.0f : 1.1f );
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
