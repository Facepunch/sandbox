//
// Bakes a merged decal's textures in one dispatch. Every texel walks the stamps that touch its tile in paint order
// and blends them the way the decal shader would draw them one after another, then writes once.
//
MODES
{
	Default();
}

CS
{
	#include "postprocess/shared.hlsl"
	#include "common/classes/Bindless.hlsl"

	// all float4 so it packs the same everywhere, DecalMerger.GpuStamp matches it
	struct Stamp
	{
		float4 Rect;		// the merged texels it covers, min xy max zw
		float4 Uv;			// its uv at merged texel 0,0 in xy, step per texel x in zw
		float4 UvY;			// step per texel y in xy, coverage amount, depth scale
		float4 Tint;
		float4 BasisX;		// rows taking its tangent space normals into the merged decal's, which is rolled differently
		float4 BasisY;
		float4 BasisZ;
		int4 Textures;		// bindless colour, normal, rmo, height, 0 where the group has no such channel
	};

	StructuredBuffer<Stamp> Stamps < Attribute( "Stamps" ); >;
	int StampCount < Attribute( "StampCount" ); >;

	RWTexture2D<float4> ColorOut < Attribute( "ColorOut" ); >;
	RWTexture2D<float4> NormalOut < Attribute( "NormalOut" ); >;
	RWTexture2D<float4> RmoOut < Attribute( "RmoOut" ); >;
	RWTexture2D<float> HeightOut < Attribute( "HeightOut" ); >;

	SamplerState Sampler < Filter( TRILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	// at the mip the decal shader would use at this size
	float4 Sample( Texture2D tex, float2 uv, Stamp stamp )
	{
		float2 size;
		tex.GetDimensions( size.x, size.y );
		return tex.SampleLevel( Sampler, uv, log2( max( length( stamp.Uv.zw * size ), length( stamp.UvY.xy * size ) ) ) );
	}

	// blend one stamp over what's under the texel, the way the decal shader would draw it on top
	void BlendStamp( Stamp stamp, float2 texel, inout float4 color, inout float3 normal, inout float3 rmo, inout float height )
	{
		if ( any( texel < stamp.Rect.xy ) || any( texel >= stamp.Rect.zw ) ) return;

		float2 uv = stamp.Uv.xy + stamp.Uv.zw * ( texel.x + 0.5 ) + stamp.UvY.xy * ( texel.y + 0.5 );
		if ( any( uv < 0 ) || any( uv > 1 ) ) return;

		// read the way the decal shader does, colour through its sRGB view, the rest as stored
		float4 c = Sample( Bindless::GetTexture2DSrgb( stamp.Textures.x ), uv, stamp ) * stamp.Tint;
		float alpha = c.a;

		float h = 1;
		if ( stamp.Textures.w > 0 )
		{
			h = Sample( Bindless::GetTexture2D( stamp.Textures.w ), uv, stamp ).r;

			// the decal shader's height coverage mask, baked in since the merged decal has none
			float amount = 1 - stamp.UvY.z;
			if ( amount > 0 )
			{
				float range = min( 0.07, min( amount, 1 - amount ) );
				alpha *= smoothstep( amount - range, amount + 0.001, h );
			}
		}

		// a texel nothing covers yet still takes this stamp's colour, so filtering and mips don't fade the edges to black
		if ( alpha <= 0 && color.a > 0 ) return;
		float total = alpha + color.a * ( 1 - alpha );
		float weight = total > 0 ? alpha / total : 1;

		color = float4( lerp( color.rgb, c.rgb, weight ), total );

		if ( stamp.Textures.y > 0 )
		{
			float3 n = normalize( Sample( Bindless::GetTexture2D( stamp.Textures.y ), uv, stamp ).xyz * 2 - 1 );
			normal = normalize( lerp( normal, float3( dot( stamp.BasisX.xyz, n ), dot( stamp.BasisY.xyz, n ), dot( stamp.BasisZ.xyz, n ) ), weight ) );
		}

		if ( stamp.Textures.z > 0 )
			rmo = lerp( rmo, Sample( Bindless::GetTexture2D( stamp.Textures.z ), uv, stamp ).rgb, weight );

		// rescaled so the merged decal's one parallax strength fits every stamp
		if ( stamp.Textures.w > 0 )
			height = lerp( height, 1 - ( 1 - h ) * stamp.UvY.w, weight );
	}

	#define TILE 8
	#define BATCH ( TILE * TILE )

	// which stamps of the current batch touch this group's tile
	groupshared bool Touches[BATCH];

	[numthreads( TILE, TILE, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID, uint3 group : SV_GroupID, uint thread : SV_GroupIndex )
	{
		uint2 size;
		ColorOut.GetDimensions( size.x, size.y );

		// threads past the edge still help cull, returning early would break the barriers
		bool inside = all( id.xy < size );
		float2 texel = id.xy;
		float2 tileMin = group.xy * TILE;
		float2 tileMax = tileMin + TILE;

		// what's under this texel so far, straight alpha like any decal texture
		float4 color = 0;
		float3 normal = float3( 0, 0, 1 );
		float3 rmo = 1;
		float height = 1;

		// the group culls a batch of stamps against its tile in parallel, one stamp per thread,
		// then every texel walks just the ones that touch it, still in paint order
		for ( int batch = 0; batch < StampCount; batch += BATCH )
		{
			int index = batch + thread;
			Touches[thread] = index < StampCount && all( Stamps[index].Rect.xy < tileMax ) && all( Stamps[index].Rect.zw > tileMin );
			GroupMemoryBarrierWithGroupSync();

			for ( int j = 0; j < BATCH && inside; j++ )
			{
				if ( Touches[j] )
					BlendStamp( Stamps[batch + j], texel, color, normal, rmo, height );
			}

			GroupMemoryBarrierWithGroupSync();
		}

		if ( !inside ) return;

		// every stamp has the same channels, only the ones there have an output bound
		int4 has = Stamps[0].Textures;
		ColorOut[id.xy] = color;
		if ( has.y > 0 ) NormalOut[id.xy] = float4( normal * 0.5 + 0.5, 1 );
		if ( has.z > 0 ) RmoOut[id.xy] = float4( rmo, 1 );
		if ( has.w > 0 ) HeightOut[id.xy] = height;
	}
}
