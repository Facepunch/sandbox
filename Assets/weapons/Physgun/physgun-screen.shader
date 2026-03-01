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

	Texture2D g_tSelfIllumMask < Attribute( "Emissive" ); >;
	Texture2D g_tGraphData < Attribute( "GraphData" ); >;

	float4 g_vGrid < Attribute( "Grid" ); Default4( 8, 6, 0.3, 0 ); >;
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
		float4 color = ShadingModelStandard::Shade( i, m );
		color.rgb *= 0.05;

		float2 uv = i.vTextureCoords.xy;
		float sampleCount = g_vGraphInfo.x;

				// Dot grid overlay
		float gridVal = DrawGrid( uv, g_vGrid.x, g_vGrid.y, g_vGrid.z );
		color.rgb += float3( gridVal * 0.05, gridVal * 0.05, gridVal * 0.05 );

		// LCD pixel grid
		float2 lcdGrid = float2( 4, 1 ) * 16;
		float2 pixUv = uv + ( 0.5 / lcdGrid );
		float2 pxMod = 1 - distance( fmod( pixUv * lcdGrid + 0.5, 1 ), 0.5 );

		// Plot channels
		float ch1 = DrawChannel( uv, 0, g_vBand1.x, g_vBand1.y, sampleCount );
		color.rgb += g_vCh1Color.rgb * g_vCh1Color.a * ch1;

		float ch2 = DrawChannel( uv, 1, g_vBand2.x, g_vBand2.y, sampleCount );
		color.rgb += g_vCh2Color.rgb * g_vCh2Color.a * ch2;

		float ch3 = DrawChannel( uv, 2, g_vBand3.x, g_vBand3.y, sampleCount );
		color.rgb += g_vCh3Color.rgb * g_vCh3Color.a * ch3;

		// Painted overlay from render target
		float3 overlay = g_tSelfIllumMask.SampleLevel( g_sPointClamp, uv, 0 ).rgb;
		color.rgb += overlay * 1.0;
		
		// LCD round pixel
		color.rgb *= saturate( pxMod.x - 0.01 );

		return color;
	}
}
