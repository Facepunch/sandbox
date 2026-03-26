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

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );

		float4 color = ShadingModelStandard::Shade( i, m );

		float2 grid = float2( 4, 1 ) * 32;
		float2 uv = i.vTextureCoords.xy + ( 0.5 / grid );
		float2 mod = 1 - distance( fmod( uv * grid + 0.5, 1 ), 0.5 );

		uv = round( uv * grid ) / grid;

		color.rgb += g_tSelfIllumMask.SampleLevel( g_sPointClamp, uv, 0 ).rgb * 5;
		color.rgb *= g_tSelfIllumMask.SampleLevel( g_sPointClamp, uv, 1 ).rgb * 2;

		float offs = 0.03;
		color.gbr += g_tSelfIllumMask.SampleLevel( g_sPointClamp, i.vTextureCoords.xy + float2( sin(g_flTime * 200) * offs, sin(g_flTime * 65) * offs * 4 ), 1 ).rgb * 0.2;
		color.brg += g_tSelfIllumMask.SampleLevel( g_sPointClamp, i.vTextureCoords.xy - float2( sin(g_flTime * 300) * offs, sin(g_flTime * 60) * offs * 4), 1 ).rgb * 0.2;

    	// Simulate strobing using a sine wave over time and screen Y
    	float strobe = sin((i.vTextureCoords.y - g_flTime * 8.0) * 2.0) * 0.5 + 0.5;

    	// Soften the strobe line
    	strobe = smoothstep(0.1, 0.9, strobe);

    	// Subtle scanline brightness
    	color.rgb += lerp(0.01, 0.04, strobe) * float3( 1, 0.8, 1 );
		

		// round pixel
		color.rgb *= saturate( mod.x - 0.2 );

		return color;
	}
}
