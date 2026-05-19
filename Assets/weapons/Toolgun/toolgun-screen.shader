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
		float2 uv = i.vTextureCoords.xy;

		//
		// Pass 1: Outer layer - the physical screen surface
		//
		Material outer = Material::From( i );
		outer.Albedo = 0.0f;
		outer.AmbientOcclusion = g_tAo.Sample( g_sAniso, uv ).r;
		outer.Roughness = g_tRoughness.Sample( g_sAniso, uv ).r;
		outer.Normal = TransformNormal( DecodeNormal( g_tNormal.Sample( g_sAniso, uv ).rgb ), i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		outer.Emission = 0.0f;

		float4 outerShaded = ShadingModelStandard::Shade( i, outer );

		//
		// Pass 2: Inner LCD layer - dot matrix with per-dot normals and roughness
		//
		float2 grid = float2( 4, 1 ) * 8;

		// Sample emissive content
		float3 emissive = g_tSelfIllumMask.SampleLevel( g_sPointClamp, uv, 0 ).rgb;

		// Dot matrix cell coordinates
		float2 cell = frac( uv * grid );
		float2 cellCentered = cell - 0.5;

		float dotRadius = 0.38;
		float dist = length( cellCentered );
		float dotMask = 1.0 - smoothstep( dotRadius - 0.08, dotRadius + 0.08, dist );

		// Per-dot hemisphere normals in tangent space
		float2 nxy = cellCentered / dotRadius;
		float nzSq = saturate( 1.0 - dot( nxy, nxy ) );
		float3 domeNormalTs = float3( nxy.x, nxy.y, sqrt( nzSq ) );
		domeNormalTs = normalize( domeNormalTs );

		// Transform dome normal to world space, blend with flat normal outside dots
		float3 domeNormalWs = TransformNormal( domeNormalTs, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		float3 lcdNormal = lerp( i.vNormalWs, domeNormalWs, dotMask );

		// Dome shading falloff
		float dome = 1.0 - smoothstep( 0.0, dotRadius, dist ) * 0.25;

		// LCD emissive color
		float3 lcdColor = emissive * dotMask * dome * 5.0f;

		// Scanline refresh sweep
		float scanline = sin( ( uv.y - g_flTime * 1.5 ) * 80.0 ) * 0.5 + 0.5;
		scanline = smoothstep( 0.3, 0.7, scanline );
		lcdColor *= lerp( 0.9, 1.0, scanline );

		// Horizontal row gaps
		float rowLine = smoothstep( 0.0, 0.06, abs( cellCentered.y ) );
		lcdColor *= lerp( 0.85, 1.0, rowLine );

		// LCD dots are glossy, gaps between are rough/invisible
		Material lcd = Material::From( i );
		lcd.Albedo = 0.0f;
		lcd.AmbientOcclusion = 1.0f;
		lcd.Roughness = lerp( 1.0, 0.25, dotMask );
		lcd.Normal = lcdNormal;
		lcd.Emission = lcdColor;

		float4 lcdShaded = ShadingModelStandard::Shade( i, lcd );

		// Composite: sum both layers
		return outerShaded + lcdShaded;
	}
}
