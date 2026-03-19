HEADER
{
	Description = "Snap Grid Overlay";
	DevShader = true;
}

MODES
{
	Forward();
}

FEATURES
{
}

COMMON
{
	#define CUSTOM_MATERIAL_INPUTS
	#include "common/shared.hlsl"
}

struct VertexInput
{
	float3 vPositionWs : POSITION < Semantic( PosXyz ); >;
	uint nInstanceTransformID : TEXCOORD13 < Semantic( InstanceTransformUv ); >;
};

struct PixelInput
{
	float3 vPositionWs : TEXCOORD0;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs : SV_Position;
	#endif

	#if ( PROGRAM == VFX_PROGRAM_PS )
		float4 vPositionSs : SV_Position;
	#endif
};

VS
{
	PixelInput MainVs( VertexInput i )
	{
		PixelInput o;
		o.vPositionWs = i.vPositionWs;
		o.vPositionPs = Position3WsToPs( i.vPositionWs );

		// bit of depth bias didn't hurt nobody
		float flProjDepth = saturate( o.vPositionPs.z / o.vPositionPs.w );
		float flBias = flProjDepth * 0.0001f;
		o.vPositionPs.z -= flBias * o.vPositionPs.w;

		return o;
	}
}

PS
{
	RenderState( DepthWriteEnable, false );
	RenderState( DepthEnable, false );
	RenderState( CullMode, NONE );
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );

	float3 GridOrigin < Attribute( "GridOrigin" ); Default3( 0, 0, 0 ); >;
	float3 GridRight < Attribute( "GridRight" ); Default3( 1, 0, 0 ); >;
	float3 GridUp < Attribute( "GridUp" ); Default3( 0, 0, 1 ); >;

	float3 AimPoint < Attribute( "AimPoint" ); Default3( 0, 0, 0 ); >;
	float MaskRadius < Attribute( "MaskRadius" ); Default( 48.0 ); >;

	float CellSize < Attribute( "CellSize" ); Default( 16.0 ); >;

	float SnapCornerX < Attribute( "SnapCornerX" ); Default( 0 ); >;
	float SnapCornerY < Attribute( "SnapCornerY" ); Default( 0 ); >;

	float4 GridColor < Attribute( "GridColor" ); Default4( 1.0, 1.0, 1.0, 0.5 ); >;
	float4 CornerColor < Attribute( "CornerColor" ); Default4( 0.2, 0.6, 1.0, 1.0 ); >;

	float2 HalfExtents < Attribute( "HalfExtents" ); Default2( 48.0, 48.0 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 p = i.vPositionWs;

		float3 offset = p - GridOrigin;
		float2 facePos = float2( dot( offset, GridRight ), dot( offset, GridUp ) );

		float2 cellUv = facePos / CellSize;

		float2 deriv = max( abs( ddx( cellUv ) ), abs( ddy( cellUv ) ) );
		float2 wrapped = abs( frac( cellUv ) - 0.5 );
		float2 lw = 1.0 * deriv;
		float2 cov = saturate( ( wrapped - ( 0.5 - lw ) ) / max( lw, 0.0001 ) );
		float grid = max( cov.x, cov.y );

		// Rectangular fade: grid eases in from the bounds edge over half a cell.
		float2 edgeDist = HalfExtents - abs( facePos );
		float edgeFade = saturate( min( edgeDist.x, edgeDist.y ) / max( CellSize * 0.5, 0.001 ) );

		float dist = length( p - AimPoint );
		float gridFade = 1.0 - saturate( dist / ( MaskRadius * 0.3 ) );
		grid *= gridFade;

		float mask = edgeFade;

		float2 cornerFace = float2( SnapCornerX, SnapCornerY ) * CellSize;
		float2 localUv = facePos - cornerFace;

		float2 facePosDdx = ddx( facePos );
		float2 facePosDdy = ddy( facePos );
		float2 px = max( abs( facePosDdx ), abs( facePosDdy ) ) * 1.5;
		float barW = px.x;
		float barH = px.y;

		float barX = saturate( ( barH - abs( localUv.y ) ) / max( barH, 0.0001 ) );
		float barY = saturate( ( barW - abs( localUv.x ) ) / max( barW, 0.0001 ) );
		float crossMask = saturate( barX + barY );

		float cornerDist = length( localUv );
		float crossFade = 1.0 - saturate( cornerDist / ( CellSize * 1.0 ) );
		float cross = crossMask * crossFade;

		float4 col = GridColor * grid;
		col = lerp( col, CornerColor, cross );
		col.a *= mask;

		if ( col.a < 0.002 ) discard;

		return col;
	}
}
