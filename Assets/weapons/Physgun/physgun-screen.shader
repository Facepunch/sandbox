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
		return FinalizeVertex( o );
	}
}

PS
{
    #include "common/pixel.hlsl"

	CreateInputTexture2D( TextureColor, Srgb, 8, "", "_color", "Material,10/10", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	CreateInputTexture2D( TextureRoughness, Linear, 8, "", "_rough", "Material,10/20", Default( 0.5 ) );

	Texture2D g_tColorMap < Channel( RGBA, Box( TextureColor ), Srgb ); SrgbRead( true ); OutputFormat( BC7 ); >;
	Texture2D g_tRoughnessMap < Channel( RGBA, Box( TextureRoughness ), Linear ); SrgbRead( false ); OutputFormat( BC7 ); >;

	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;

	float g_flEmissionPower < UiGroup( "Material,10/30" ); Default1( 1.0 ); Range1( 0, 10 ); >;

	Texture2D g_tSelfIllumMask < Attribute( "Emissive" ); >;
	Texture2D g_tGraphData < Attribute( "GraphData" ); >;

	float4 g_vGrid < Attribute( "Grid" ); Default4( 8, 5, 0.1, 0 ); >;
	float4 g_vGraphInfo < Attribute( "GraphInfo" ); Default4( 128, 0, 0, 0 ); >;

	float4 g_vCh1Color < Attribute( "Ch1Color" ); Default4( 0, 1, 1, 1 ); >;
	float4 g_vCh2Color < Attribute( "Ch2Color" ); Default4( 1, 1, 0, 1 ); >;
	float4 g_vCh3Color < Attribute( "Ch3Color" ); Default4( 0, 0.8, 0.2, 1 ); >;

	float4 g_vBand1 < Attribute( "Band1" ); Default4( 0.0, 0.33, 0, 0 ); >;
	float4 g_vBand2 < Attribute( "Band2" ); Default4( 0.33, 0.33, 0, 0 ); >;
	float4 g_vBand3 < Attribute( "Band3" ); Default4( 0.66, 0.34, 0, 0 ); >;

	float DrawGrid( float2 uv, float divisionsX, float divisionsY, float brightness )
	{
		float lineWidth = 0.01;

		float gridX = abs( frac( uv.x * divisionsX + 0.5 ) - 0.5 );
		float lineX = step( gridX, lineWidth * divisionsX );

		float gridY = abs( frac( uv.y * divisionsY + 0.5 ) - 0.5 );
		float lineY = step( gridY, lineWidth * divisionsY );

		return max( lineX, lineY ) * brightness;
	}

	float ReadSample( float sampleX, int channel )
	{
		// Read from row 0 of the texture (data row)
		float2 dataUV = float2( sampleX, 0.005 );
		float3 data = g_tGraphData.SampleLevel( g_sPointClamp, dataUV, 0 ).rgb;
		if ( channel == 0 ) return data.r;
		if ( channel == 1 ) return data.g;
		return data.b;
	}

	float DrawChannel( float2 uv, int channel, float bandTop, float bandHeight, float sampleCount )
	{
		float pixelStep = 1.0 / sampleCount;

		// Current and previous sample
		float val = ReadSample( uv.x, channel );
		float prevVal = ReadSample( uv.x - pixelStep, channel );

		float lineY = bandTop + bandHeight * ( 1.0 - val );
		float prevLineY = bandTop + bandHeight * ( 1.0 - prevVal );

		float thickness = 0.012;

		// Horizontal segment at current value
		float hLine = step( abs( uv.y - lineY ), thickness );

		// Fill between previous and current value (vertical connector)
		float minY = min( prevLineY, lineY );
		float maxY = max( prevLineY, lineY );
		float localX = frac( uv.x * sampleCount );
		float vLine = step( localX, 0.12 ) * step( minY - thickness, uv.y ) * step( uv.y, maxY + thickness );

		return max( hLine, vLine );
	}


	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );

		float2 uv = i.vTextureCoords.xy;
		float sampleCount = g_vGraphInfo.x;
		float3 lcd = float3( 0, 0, 0 );

		// Dot grid overlay
		float gridVal = DrawGrid( uv, g_vGrid.x, g_vGrid.y, g_vGrid.z );
		lcd += float3( gridVal * 0.05, gridVal * 0.05, gridVal * 0.05 );

		// Plot channels
		float ch1 = DrawChannel( uv, 0, g_vBand1.x, g_vBand1.y, sampleCount );
		lcd += g_vCh1Color.rgb * g_vCh1Color.a * ch1;

		float ch2 = DrawChannel( uv, 1, g_vBand2.x, g_vBand2.y, sampleCount );
		lcd += g_vCh2Color.rgb * g_vCh2Color.a * ch2;

		float ch3 = DrawChannel( uv, 2, g_vBand3.x, g_vBand3.y, sampleCount );
		lcd += g_vCh3Color.rgb * g_vCh3Color.a * ch3;

		// Painted overlay from render target
		float3 overlay = g_tSelfIllumMask.SampleLevel( g_sPointClamp, uv, 0 ).rgb;
		lcd += overlay * 1.0;

		// Save pre-LCD color for distance blending
		float3 rawLcd = lcd;

		float2 lcdRes = float2( 64, 48 );
		float2 cellUv = frac( uv * lcdRes );

		// Each pixel cell has 3 vertical RGB sub-pixel columns
		float subX = cellUv.x * 3.0;
		float subLocal = frac( subX );
		float subIdx = floor( subX );

		// Rounded shape with visible gaps between columns and rows
		float subMask = smoothstep( 0.0, 0.2, subLocal ) * smoothstep( 1.0, 0.8, subLocal );
		float rowMask = smoothstep( 0.0, 0.15, cellUv.y ) * smoothstep( 1.0, 0.85, cellUv.y );
		float shape = subMask * rowMask;

		// Tint each sub-pixel strip to its primary color, slight bleed to neighbors
		float3 subColor;
		subColor.r = ( subIdx == 0 ) ? 1.0 : 0.06;
		subColor.g = ( subIdx == 1 ) ? 1.0 : 0.06;
		subColor.b = ( subIdx == 2 ) ? 1.0 : 0.06;

		// Apply LCD
		lcd *= subColor * shape * 2.8;

		// Faint backlight bleed visible in the gaps
		lcd += 0.002;

		// Fade out grid at distance
		float2 cellScreenSize = fwidth( uv * lcdRes );
		float lcdFade = 1.0 - saturate( max( cellScreenSize.x, cellScreenSize.y ) * 2.0 );
		lcd = lerp( rawLcd, lcd, lcdFade );

		float3 viewDir = normalize( g_vCameraPositionWs.xyz - ( i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz ) );
		float NdotV = saturate( dot( normalize( i.vNormalWs ), viewDir ) );

		// Sharp falloff
		float angleDim = pow( NdotV, 5.0 );

		// At extreme angles, colors wash out toward a dim bluish backlight tint
		float3 washout = float3( 0.008, 0.008, 0.012 );
		lcd = lerp( washout, lcd, angleDim );

		m.Albedo = Tex2DS( g_tColorMap, g_sSampler0, i.vTextureCoords.xy ).rgb;
		m.Emission = lcd * g_flEmissionPower;
		m.Roughness = Tex2DS( g_tRoughnessMap, g_sSampler0, i.vTextureCoords.xy ).r;
		m.Metalness = 0.0;

		return ShadingModelStandard::Shade( i, m );
	}
}