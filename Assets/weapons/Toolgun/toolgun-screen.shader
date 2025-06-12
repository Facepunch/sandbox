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

		float2 grid = float2( 4, 1 ) * 16;
		float2 uv = i.vTextureCoords.xy + ( 0.5 / grid );
		float2 mod = 1 - distance( fmod( uv * grid + 0.5, 1 ), 0.5 );

		uv = round( uv * grid ) / grid;

		color.rgb += g_tSelfIllumMask.SampleLevel( g_sPointClamp, uv, 0 ).rgb * 10;

		// round pixel
		color.rgb *= saturate( (mod.x - 0.5) * 5 );

		return color;
	}
}
