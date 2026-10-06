MODES
{
	Default();
	Forward();
}

FEATURES
{
	#include "ui/features.hlsl"
}

COMMON
{
	#include "ui/common.hlsl"
}

VS
{
	#include "ui/vertex.hlsl"
}

PS
{
	#include "ui/pixel.hlsl"

	// HUD
	Texture2D 	g_tHud 			< Attribute( "HudTexture" ); SrgbRead( false ); >;
	float2 		g_vHudInvSize	< Attribute( "HudInvSize" ); >;

	// frame buffer
	BoolAttribute( bWantsFBCopyTexture, true );
	Texture2D 	g_tFrame 		< Attribute( "FrameBufferCopyTexture" ); SrgbRead( true ); >;
	float4 		g_vFrameRect 	< Attribute( "FrameBufferCopyRectangle" ); Default4( 0.0, 0.0, 1.0, 1.0 ); >;

	// glow settings
	Texture2D g_tGlow < Attribute( "GlowTexture" ); SrgbRead( false ); >;
	float GlowStrength < Default( 0.15 ); Attribute( "GlowStrength" ); >;	// how fast HDR excess turns into glow
	float ScanlineIntensity < Default( 0.35 ); Attribute( "ScanlineIntensity" ); >;
	float ScanlinePeriod < Default( 3.0 ); Attribute( "ScanlinePeriod" ); >;		// screen pixels from one line to the next
	float ScanlineThickness < Default( 0.4 ); Attribute( "ScanlineThickness" ); >;	// share of the period that is dark
	float ScanlineSoftness < Default( 1 ); Attribute( "ScanlineSoftness" ); >;	// edge blur, as a share of the period

	RenderState( ColorWriteEnable0, RGBA );
	RenderState( FillMode, SOLID );
	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );

	float4 SampleHud( float2 uv )
	{
		float4 s = g_tHud.SampleLevel( g_sTrilinearBorder, uv, 0 );
		float3 c = s.a > 0.00001 ? SrgbGammaToLinear( s.rgb / s.a ) : 0;
		return float4( c, s.a );
	}

	float3 Glow( float2 uv )
	{
		float3 vExcess = g_tGlow.SampleLevel( g_sTrilinearClamp, uv, 0 ).rgb;
		return 1.0 - exp( -vExcess * GlowStrength );
	}

	float ScanlineMask( float flPixelY )
	{
		float t = frac( flPixelY / max( ScanlinePeriod, 1.0 ) );
		float flHalf = ScanlineThickness * 0.5;
		return 1.0 - smoothstep( flHalf - ScanlineSoftness, flHalf + ScanlineSoftness, abs( t - 0.5 ) );
	}

	float4 MainPs( PS_INPUT i ) : SV_Target0
	{
		float2 vPixel = i.vPositionPs.xy;
		float2 vHudUv = vPixel * g_vHudInvSize;

		float2 vNdc = i.vPositionSs.xy / i.vPositionSs.w;
		float2 vFrameUv = ( float2( vNdc.x, -vNdc.y ) * 0.5 + 0.5 ) * g_vFrameRect.zw;
		float3 vBackdrop = g_tFrame.SampleLevel( g_sTrilinearClamp, vFrameUv, 0 ).rgb;
		if ( !g_bUIFrameGrabEncoded || g_bUIInPanelLayer )
			vBackdrop = SrgbLinearToGamma( vBackdrop );

		// additive glow
		float3 vGlow = Glow( vHudUv );
		float3 vResult = SrgbLinearToGamma( saturate( SrgbGammaToLinear( saturate( vBackdrop ) ) + vGlow ) );

		// HUD
		float4 vHud = SampleHud( vHudUv );
		float3 vHudColor = saturate( vHud.rgb );
		float flHudCoverage = saturate( vHud.a );
		vResult = lerp( vResult, SrgbLinearToGamma( vHudColor ), flHudCoverage );

		// scanlines
		float3 vLinear = SrgbGammaToLinear( vResult );
		vLinear += ScanlineMask( vPixel.y ) * saturate( ScanlineIntensity ) * ( vHudColor * flHudCoverage + vGlow );

		// The UI layer blends in gamma space, so encode when it asks for that
		return UIEncodeOutput( float4( saturate( vLinear ), 1.0 ) );
	}
}
