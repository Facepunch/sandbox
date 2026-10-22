//
// Composites one decal stamp over a merged decal's textures, the same way the decal shader would draw it on top.
// DecalMerger dispatches it once per stamp, in paint order.
//
MODES
{
	Default();
}

CS
{
	#include "postprocess/shared.hlsl"

	// read the way the decal shader does, colour decoded from sRGB, the rest as stored
	Texture2D Color < Attribute( "Color" ); SrgbRead( true ); >;
	Texture2D Normal < Attribute( "Normal" ); SrgbRead( false ); >;
	Texture2D Rmo < Attribute( "Rmo" ); SrgbRead( false ); >;
	Texture2D Height < Attribute( "Height" ); SrgbRead( false ); >;
	SamplerState Sampler < Filter( TRILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	RWTexture2D<float4> ColorOut < Attribute( "ColorOut" ); >;
	RWTexture2D<float4> NormalOut < Attribute( "NormalOut" ); >;
	RWTexture2D<float4> RmoOut < Attribute( "RmoOut" ); >;
	RWTexture2D<float4> HeightOut < Attribute( "HeightOut" ); >;

	// the merged texels this stamp covers
	float2 Offset < Attribute( "Offset" ); >;
	float2 Size < Attribute( "Size" ); >;

	// the stamp's uv at merged texel 0,0 and per texel, it's affine
	float2 Origin < Attribute( "Origin" ); >;
	float2 StepX < Attribute( "StepX" ); >;
	float2 StepY < Attribute( "StepY" ); >;

	float4 Tint < Attribute( "Tint" ); >;

	// rows taking the stamp's tangent space normals into the merged decal's, which is rolled differently
	float3 BasisX < Attribute( "BasisX" ); >;
	float3 BasisY < Attribute( "BasisY" ); >;
	float3 BasisZ < Attribute( "BasisZ" ); >;

	float CoverageAmount < Attribute( "CoverageAmount" ); Default( 1 ); >;
	float DepthScale < Attribute( "DepthScale" ); Default( 1 ); >;

	int HasNormal < Attribute( "HasNormal" ); >;
	int HasRmo < Attribute( "HasRmo" ); >;
	int HasHeight < Attribute( "HasHeight" ); >;

	// the mip the decal shader would sample at this size
	float Lod( Texture2D tex )
	{
		float2 size;
		tex.GetDimensions( size.x, size.y );
		return log2( max( length( StepX * size ), length( StepY * size ) ) );
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( any( id.xy >= (uint2)Size ) ) return;

		uint2 texel = (uint2)Offset + id.xy;
		float2 uv = Origin + StepX * ( texel.x + 0.5 ) + StepY * ( texel.y + 0.5 );
		if ( any( uv < 0 ) || any( uv > 1 ) ) return;

		float4 color = Color.SampleLevel( Sampler, uv, Lod( Color ) ) * Tint;
		float alpha = color.a;

		float height = 1;
		if ( HasHeight != 0 )
		{
			height = Height.SampleLevel( Sampler, uv, Lod( Height ) ).r;

			// the decal shader's height coverage mask, baked in since the merged decal has none
			float amount = 1 - CoverageAmount;
			if ( amount > 0 )
			{
				float range = min( 0.07, min( amount, 1 - amount ) );
				alpha *= smoothstep( amount - range, amount + 0.001, height );
			}
		}

		if ( alpha <= 0 ) return;

		// over what's baked so far, kept straight alpha like any decal texture
		float4 below = ColorOut[texel];
		float total = alpha + below.a * ( 1 - alpha );
		float weight = alpha / total;

		ColorOut[texel] = float4( lerp( below.rgb, color.rgb, weight ), total );

		if ( HasNormal != 0 )
		{
			float3 normal = normalize( Normal.SampleLevel( Sampler, uv, Lod( Normal ) ).xyz * 2 - 1 );
			normal = float3( dot( BasisX, normal ), dot( BasisY, normal ), dot( BasisZ, normal ) );
			NormalOut[texel] = float4( normalize( lerp( NormalOut[texel].xyz * 2 - 1, normal, weight ) ) * 0.5 + 0.5, 1 );
		}

		if ( HasRmo != 0 )
		{
			float3 rmo = Rmo.SampleLevel( Sampler, uv, Lod( Rmo ) ).rgb;
			RmoOut[texel] = float4( lerp( RmoOut[texel].rgb, rmo, weight ), 1 );
		}

		if ( HasHeight != 0 )
		{
			// rescaled so the merged decal's one parallax strength fits every stamp
			float scaled = 1 - ( 1 - height ) * DepthScale;
			HeightOut[texel] = float4( lerp( HeightOut[texel].rrr, scaled.xxx, weight ), 1 );
		}
	}
}
