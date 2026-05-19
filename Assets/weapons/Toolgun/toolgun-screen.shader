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
	CreateInputTexture2D( TextureAmbientOcclusion, Linear, 8, "", "_ao", "Material,10/Ambient Occlusion", Default( 1.0 ) );
	CreateInputTexture2D( TextureRoughness, Linear, 8, "", "_rough", "Material,10/Roughness", Default( 0.5 ) );
	CreateInputTexture2D( TextureNormal, Linear, 8, "NormalizeNormals", "_normal", "Material,10/Normal", Default3( 0.5, 0.5, 1.0 ) );
	CreateTexture2D( g_tAo ) < Channel( R, Box( TextureAmbientOcclusion ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	CreateTexture2D( g_tRoughness ) < Channel( R, Box( TextureRoughness ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	CreateTexture2D( g_tNormal ) < Channel( RGB, Box( TextureNormal ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );

		// Dot matrix grid resolution
		float2 grid = float2( 4, 1 ) * 8;
		float2 uv = i.vTextureCoords.xy;

		// Snap UV to nearest dot center for pixelated sampling
		float2 snappedUv = ( floor( uv * grid ) + 0.5 ) / grid;

		// Sample the emissive texture at the snapped dot center
        float3 emissive = g_tSelfIllumMask.SampleLevel(g_sPointClamp, uv, 0).rgb;
        emissive = max(emissive, float3(0.005,0.005,0.00f)); // Ensure a minimum glow for visible dots

		// Dot matrix cell coordinates [0,1] within each dot
		float2 cell = frac( uv * grid );
		float2 cellCentered = cell - 0.5;

		// Circular dot mask - each pixel is a soft round dot
		float dotRadius = 0.38;
		float dist = length( cellCentered );
		float dot = 1.0 - smoothstep( dotRadius - 0.08, dotRadius + 0.08, dist );

		// Subtle dome shading on each dot for that raised LCD bump look
		float dome = 1.0 - smoothstep( 0.0, dotRadius, dist ) * 0.25;

		// Build the LCD pixel color: emissive content on dot, dark between dots
		float3 lcdColor = emissive * dot * dome * 5.0f;

		// Slow scanline refresh sweep - subtle brightness band rolling down
		float scanline = sin( ( uv.y - g_flTime * 1.5 ) * 80.0 ) * 0.5 + 0.5;
		scanline = smoothstep( 0.3, 0.7, scanline );
		lcdColor *= lerp( 0.92, 1.0, scanline );

		// Very subtle horizontal line structure (LCD row gaps)
		float rowLine = smoothstep( 0.0, 0.06, abs( cellCentered.y ) );
		lcdColor *= lerp( 0.85, 1.0, rowLine );

        m.Albedo = 0.0f;
        m.AmbientOcclusion = g_tAo.Sample( g_sAniso, i.vTextureCoords.xy ).r;
        m.Roughness = g_tRoughness.Sample( g_sAniso, i.vTextureCoords.xy ).r;
        m.Normal = TransformNormal( DecodeNormal( g_tNormal.Sample( g_sAniso, i.vTextureCoords.xy ).rgb ), i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
        m.Emission = lcdColor.rgb;
		return ShadingModelStandard::Shade( i, m );
	}
}
