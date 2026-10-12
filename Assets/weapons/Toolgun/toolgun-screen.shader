FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    Forward();
    Depth();
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		// Add your vertex manipulation functions here
		return FinalizeVertex( o );
	}
}

PS
{
    #include "common/pixel.hlsl"

	Texture2D g_tSelfIllumMask < Attribute( "Emissive" ); >;

	// the glass and the four LCDs under it, handed over from the model's own screen material by ScreenWeapon
	Texture2D g_tPanelColor < Attribute( "PanelColor" ); >;
	Texture2D g_tPanelNormal < Attribute( "PanelNormal" ); >;
	Texture2D g_tPanelAo < Attribute( "PanelAmbientOcclusion" ); >;

	SamplerState g_sPanel < Filter( ANISO ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	SamplerState g_sScreen < Filter( BILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	// the panel is four square LCDs side by side under one sheet of glass, the picture spans all four
	static const float Panels = 4.0;
	static const float PanelGap = 0.035;		// dark border round each LCD, as a share of its width

	// LCD cells across the whole strip, 4 texels each on the 512x128 picture
	static const float CellsX = 128.0;
	static const float CellsY = 32.0;

	// HDR excess turns into glow, like the HUD composite (shaders/hud_composite.shader)
	static const float GlowStrength = 0.22;
	static const int GlowTaps = 32;
	static const float GlowRadius = 14.0;		// in screen texels

	// brightness of the picture, flared above 1 by ScreenWeapon while the tool changes
	float g_flIntensity < Attribute( "ScreenIntensity" ); Default( 1.0 ); >;

	float PanelMask( float2 uv )
	{
		float2 local = float2( frac( uv.x * Panels ), uv.y );
		float2 edge = smoothstep( 0.0, PanelGap, local ) * smoothstep( 1.0, 1.0 - PanelGap, local );
		return edge.x * edge.y;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );

		float2 size;
		g_tSelfIllumMask.GetDimensions( size.x, size.y );

		float2 uv = i.vTextureCoords.xy;

		// each LCD cell shows the picture at its centre, a soft rounded dot with faint red/green/blue columns
		float2 cells = float2( CellsX, CellsY );
		float2 cellUv = uv * cells;
		float2 inCell = frac( cellUv );
		float3 picture = g_tSelfIllumMask.SampleLevel( g_sPointClamp, ( floor( cellUv ) + 0.5 ) / cells, 0 ).rgb;

		float2 cellDot = smoothstep( 0.0, 0.18, inCell ) * smoothstep( 1.0, 0.82, inCell );
		float3 columns = float3( inCell.x < 0.34, inCell.x >= 0.34 && inCell.x < 0.67, inCell.x >= 0.67 );
		float3 subpixel = lerp( 1.0, columns * 3.0, 0.15 );

		// blurred HDR excess like the HUD's glow pass: a gaussian over a golden-angle spiral of taps, so the halo is
		// smooth instead of picking out the shape of a few rings
		float3 excess = 0;
		float weight = 0;
		[unroll]
		for ( int k = 0; k < GlowTaps; k++ )
		{
			float t = ( k + 0.5 ) / GlowTaps;
			float r = sqrt( t ) * GlowRadius;
			float a = k * 2.39996;
			float w = exp( -t * 3.0 );
			excess += max( g_tSelfIllumMask.SampleLevel( g_sScreen, uv + float2( cos( a ), sin( a ) ) * r / size, 0 ).rgb - 1.0, 0 ) * w;
			weight += w;
		}
		excess /= weight;
		float3 glow = 1.0 - exp( -excess * GlowStrength * 6 );

		// the HUD's scanlines run through the glow too, one dark line per row of LCD cells
		glow *= lerp( 0.55, 1.0, cellDot.y );

		float3 lcd = saturate( picture ) * cellDot.x * cellDot.y * subpixel * 2.0 + glow * 0.6;

		// the light sits under the glass: the panel art keeps its shape and reflections on top, but the glass itself
		// is glossy and the LCDs under it near black, so bright light shows up as a sheen rather than greying the panels
		float2 artUv = i.vTextureCoords.xy;
		float4 normalRough = g_tPanelNormal.Sample( g_sPanel, artUv );
		m.Albedo = g_tPanelColor.Sample( g_sPanel, artUv ).rgb * 0.2;
		m.Normal = TransformNormal( DecodeNormal( normalRough.rgb ), i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		m.Roughness = 0.12;
		m.AmbientOcclusion = g_tPanelAo.Sample( g_sPanel, artUv ).r;
		m.Metalness = 0;
		m.Emission = lcd * PanelMask( uv ) * g_flIntensity;
		return ShadingModelStandard::Shade( i, m );
	}
}
