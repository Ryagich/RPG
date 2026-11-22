// Project-owned Synty vegetation variant. Shared bending lives in GrassDeformation.hlsl.
// Edit this source directly; the upstream Amplify graph is intentionally not embedded.
// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "RPG/World/Interactive Grass"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[ASEBegin][Header(Leaves)]_LeafTex("LeafTex", 2D) = "white" {}
		_LeafNormalMap("LeafNormalMap", 2D) = "bump" {}
		_LeafNormalAmount("LeafNormalAmount", Range( 0 , 1)) = 0.5
		_LeafMetallic("LeafMetallic", Range( 0 , 1)) = 0.13
		_LeafSmoothness("LeafSmoothness", Range( 0 , 1)) = 0.5
		_LeafAmbientOcclusion("LeafAmbientOcclusion", 2D) = "white" {}
		[Header(Trunk)]_TunkTex("TunkTex", 2D) = "white" {}
		_TrunkNormalMap("TrunkNormalMap", 2D) = "bump" {}
		_TrunkNormalAmount("TrunkNormalAmount", Range( 0 , 1)) = 0.5
		_TrunkMetallic("TrunkMetallic", Range( 0 , 1)) = 0
		_TrunkSmoothness("TrunkSmoothness", Range( 0 , 1)) = 0.2
		_TrunkAmbientOcclusion("TrunkAmbientOcclusion", 2D) = "white" {}
		[Header(Colour Tinting)]_LeafBaseColour("LeafBaseColour", Color) = (0.07843138,0.02015968,0,0)
		_LeafNoiseColour("LeafNoiseColour", Color) = (0.07843138,0.02015968,0,0)
		_LeafNoiseLargeColour("LeafNoiseLargeColour", Color) = (0.07843138,0.02015968,0,0)
		[Toggle]_LeafFlatColourSwitch("LeafFlatColourSwitch", Float) = 0
		_TrunkBaseColour("TrunkBaseColour", Color) = (0.07843138,0.02015968,0,0)
		_TrunkNoiseColour("TrunkNoiseColour", Color) = (0.07843138,0.02015968,0,0)
		[Toggle]_TrunkFlatColourSwitch("TrunkFlatColourSwitch", Float) = 0
		_ColourNoiseSmallScale("ColourNoiseSmallScale", Range( 0 , 1)) = 0
		_ColourNoiseLargeScale("ColourNoiseLargeScale", Range( 0 , 1)) = 0
		[Header(Emissive)]_EmissiveAmount("EmissiveAmount", Range( 0 , 2)) = 0
		_EmissiveMask("EmissiveMask", 2D) = "white" {}
		_EmissiveColour("EmissiveColour", Color) = (0,0,0,0)
		_Emissive2Mask("Emissive2Mask", 2D) = "white" {}
		_Emissive2Colour("Emissive2Colour", Color) = (0,0,0,0)
		_TrunkEmissiveColour("TrunkEmissiveColour", Color) = (0,0,0,0)
		_TrunkEmissiveMask("TrunkEmissiveMask", 2D) = "white" {}
		_EmissivePulseMap("EmissivePulseMap", 2D) = "white" {}
		[Header(Main Wind)]_TotalWindAmount("TotalWindAmount", Range( 0 , 1)) = 0.5
		_WindDirection("WindDirection", Range( 0 , 1)) = 0
		_WindBaseline("WindBaseline", Range( 0 , 3)) = 0
		_Y_multiplier("Y_multiplier", Range( -2 , 2)) = 0.2
		_GustAmount("GustAmount", Range( 0 , 1)) = 0.5
		_GustSmallFreq("GustSmallFreq", Range( 0 , 1)) = 1
		_GustLargeFreq("GustLargeFreq", Range( 0 , 1)) = 0.5
		_GustNoiseMap("GustNoiseMap", 2D) = "white" {}
		_GustScale("GustScale", Range( 0 , 1)) = 0.5
		_GustHighlight("GustHighlight", Color) = (0,0,0,0)
		[Header(LeafSway)]_LeafBigNoiseScale("LeafBigNoiseScale", Range( 0 , 10)) = 1
		_LeafBigNoiseAmount("LeafBigNoiseAmount", Range( 0 , 1)) = 0.5
		[Header(LeafJitter)]_JitterFreq("JitterFreq", Range( 0 , 1)) = 0
		_JitterAmount("JitterAmount", Range( 0 , 1)) = 0.5
		_Leaves_NoiseTexture2("LeafJitterMap", 2D) = "white" {}
		[Header(Frosting)]_FrostingColour("FrostingColour", Color) = (1,1,1,0)
		[Toggle]_FrostingSwitch("FrostingSwitch", Float) = 0
		_FrostingHeight("FrostingHeight", Float) = 1
		_FrostingFalloff("FrostingFalloff", Float) = 1
		[Header(LOD Crossfade)]_LOD_DitheringMap("LOD_DitheringMap", 2D) = "white" {}
		[Toggle(LOD_FADE_CROSSFADE)] _LOD_FADE_CROSSFADE_out("LOD_FADE_CROSSFADE_out", Float) = 1
		[ASEEnd][Toggle(LOD_FADE_CROSSFADE)] _LOD_FADE_CROSSFADE_in("LOD_FADE_CROSSFADE_in", Float) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		//_TransStrength( "Trans Strength", Range( 0, 50 ) ) = 1
		//_TransNormal( "Trans Normal Distortion", Range( 0, 1 ) ) = 0.5
		//_TransScattering( "Trans Scattering", Range( 1, 50 ) ) = 2
		//_TransDirect( "Trans Direct", Range( 0, 1 ) ) = 0.9
		//_TransAmbient( "Trans Ambient", Range( 0, 1 ) ) = 0.1
		//_TransShadow( "Trans Shadow", Range( 0, 1 ) ) = 0.5
		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25
	}

	SubShader
	{
		LOD 0

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
		Cull Off
		AlphaToMask Off
		
		HLSLINCLUDE
		#include "GrassDeformation.hlsl"
		#pragma target 3.5

		#pragma prefer_hlslcc gles
		#pragma exclude_renderers d3d11_9x 


		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}
		
		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }
			
			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM
			
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 70701

			
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_FORWARD

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			
			#if ASE_SRP_VERSION <= 70108
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
			#endif

			#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
			    #define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_SCREEN_POSITION
			#pragma shader_feature_local LOD_FADE_CROSSFADE


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 lightmapUVOrVertexSH : TEXCOORD0;
				half4 fogFactorAndVertexLight : TEXCOORD1;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord : TEXCOORD2;
				#endif
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
				#if defined(ASE_NEEDS_FRAG_SCREEN_POSITION)
				float4 screenPos : TEXCOORD6;
				#endif
				float4 ase_color : COLOR;
				float4 ase_texcoord7 : TEXCOORD7;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FrostingColour;
			float4 _LeafAmbientOcclusion_ST;
			float4 _TrunkEmissiveMask_ST;
			float4 _TrunkEmissiveColour;
			float4 _Emissive2Mask_ST;
			float4 _Emissive2Colour;
			float4 _EmissiveMask_ST;
			float4 _EmissiveColour;
			float4 _TrunkNormalMap_ST;
			float4 _LeafNormalMap_ST;
			float4 _TrunkBaseColour;
			float4 _TrunkNoiseColour;
			float4 _TrunkAmbientOcclusion_ST;
			float4 _GustHighlight;
			float4 _LeafNoiseLargeColour;
			float4 _LeafBaseColour;
			float4 _LOD_DitheringMap_TexelSize;
			float4 _TunkTex_ST;
			float4 _LeafTex_ST;
			float4 _LeafNoiseColour;
			float _JitterFreq;
			float _TrunkSmoothness;
			float _LeafSmoothness;
			float _TrunkMetallic;
			float _LeafMetallic;
			float _EmissiveAmount;
			float _LeafBigNoiseAmount;
			float _LeafBigNoiseScale;
			float _WindDirection;
			float _GustAmount;
			float _GustScale;
			float _GustSmallFreq;
			float _TrunkNormalAmount;
			float _LeafNormalAmount;
			float _WindBaseline;
			float _TotalWindAmount;
			float _Y_multiplier;
			float _TrunkFlatColourSwitch;
			float _FrostingFalloff;
			float _FrostingHeight;
			float _FrostingSwitch;
			float _LeafFlatColourSwitch;
			float _ColourNoiseLargeScale;
			float _ColourNoiseSmallScale;
			float _GustLargeFreq;
			float _JitterAmount;
			#ifdef _TRANSMISSION_ASE
				float _TransmissionShadow;
			#endif
			#ifdef _TRANSLUCENCY_ASE
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _Leaves_NoiseTexture2;
			sampler2D _GustNoiseMap;
			sampler2D _LeafTex;
			sampler2D _TunkTex;
			sampler2D _LeafNormalMap;
			sampler2D _TrunkNormalMap;
			sampler2D _EmissiveMask;
			sampler2D _Emissive2Mask;
			sampler2D _TrunkEmissiveMask;
			sampler2D _EmissivePulseMap;
			sampler2D _LeafAmbientOcclusion;
			sampler2D _TrunkAmbientOcclusion;
			sampler2D _LOD_DitheringMap;


			float4 CalculateContrast( float contrastValue, float4 colorTarget )
			{
				float t = 0.5 * ( 1.0 - contrastValue );
				return mul( float4x4( contrastValue,0,0,t, 0,contrastValue,0,t, 0,0,contrastValue,t, 0,0,0,1 ), colorTarget );
			}
			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			inline float DitherNoiseTex( float4 screenPos, sampler2D noiseTexture, float4 noiseTexelSize )
			{
				float dither = tex2Dlod( noiseTexture, float4(screenPos.xy * _ScreenParams.xy * noiseTexelSize.xy, 0, 0) ).g;
				float ditherRate = noiseTexelSize.x * noiseTexelSize.y;
				dither = ( 1 - ditherRate ) * dither + ditherRate;
				return dither;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float mulTime28 = _TimeParameters.x * (0.0 + (_JitterFreq - 0.0) * (0.06 - 0.0) / (1.0 - 0.0));
				float2 temp_cast_0 = (( v.vertex.xyz.x + mulTime28 )).xx;
				float lerpResult50 = lerp( tex2Dlod( _Leaves_NoiseTexture2, float4( temp_cast_0, 0, 0.0) ).r , 0.0 , ( 1.0 - v.ase_color.g ));
				float3 appendResult55 = (float3(lerpResult50 , 0.0 , 0.0));
				float4 Jitter68 = CalculateContrast((0.0 + (_JitterAmount - 0.0) * (0.3 - 0.0) / (1.0 - 0.0)),float4( (appendResult55).xz, 0.0 , 0.0 ));
				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float grayscale304 = Luminance(ase_worldPos);
				float3 temp_cast_2 = (grayscale304).xxx;
				float simplePerlin3D117 = snoise( temp_cast_2 );
				simplePerlin3D117 = simplePerlin3D117*0.5 + 0.5;
				float lerpResult130 = lerp( ( simplePerlin3D117 * sin( ( _LeafBigNoiseScale * ( grayscale304 + _TimeParameters.x ) ) ) ) , 0.0 , ( 1.0 - v.ase_color.g ));
				float cos103 = cos( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin103 = sin( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator103 = mul( float2( 1,1 ) - float2( 0,0 ) , float2x2( cos103 , -sin103 , sin103 , cos103 )) + float2( 0,0 );
				float2 temp_output_362_0 = (ase_worldPos).xz;
				float simplePerlin2D368 = snoise( temp_output_362_0*0.05 );
				simplePerlin2D368 = simplePerlin2D368*0.5 + 0.5;
				float cos361 = cos( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin361 = sin( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator361 = mul( temp_output_362_0 - float2( 0,0 ) , float2x2( cos361 , -sin361 , sin361 , cos361 )) + float2( 0,0 );
				float grayscale346 = Luminance(float3( rotator361 ,  0.0 ));
				float2 temp_cast_4 = (( sin( _TimeParameters.x * 0.25 ) * (0.0 + (_GustLargeFreq - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) )).xx;
				float temp_output_177_0 = ( (0.0 + (_GustAmount - 0.0) * (4.0 - 0.0) / (1.0 - 0.0)) * ( ( ( simplePerlin2D368 * sin( ( ( grayscale346 * _GustScale ) + ( _TimeParameters.x * -(0.0 + (_GustSmallFreq - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) ) ) ) ) + 0.5 ) / 2.0 ) * ( ( tex2Dlod( _GustNoiseMap, float4( temp_cast_4, 0, 0.0) ).r + 0.5 ) / 2.0 ) );
				float lerpResult101 = lerp( ( temp_output_177_0 + _WindBaseline ) , 0.0 , ( 1.0 - v.ase_color.r ));
				float2 break99 = ( rotator103 * lerpResult101 );
				float3 appendResult100 = (float3(break99.x , 0.0 , break99.y));
				float temp_output_293_0 = (0.0 + (_TotalWindAmount - 0.0) * (2.0 - 0.0) / (1.0 - 0.0));
				float3 break169 = ( ( _LeafBigNoiseAmount * lerpResult130 ) + ( appendResult100 * temp_output_293_0 ) );
				float smoothstepResult167 = smoothstep( 0.0 , 2.0 , abs( lerpResult101 ));
				float3 appendResult168 = (float3(break169.x , ( smoothstepResult167 * _Y_multiplier * temp_output_293_0 ) , break169.z));
				float3 worldToObjDir239 = mul( GetWorldToObjectMatrix(), float4( appendResult168, 0 ) ).xyz;
				float3 Wind201 = worldToObjDir239;
				
				o.ase_color = v.ase_color;
				o.ase_texcoord7.xy = v.texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord7.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( Jitter68 + float4( Wind201 , 0.0 ) ).rgb;
				vertexValue += mul(GetWorldToObjectMatrix(), float4(RpgGrassOffset(ase_worldPos, v.ase_color.g), 0)).xyz;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float3 positionVS = TransformWorldToView( positionWS );
				float4 positionCS = TransformWorldToHClip( positionWS );

				VertexNormalInputs normalInput = GetVertexNormalInputs( v.ase_normal, v.ase_tangent );

				o.tSpace0 = float4( normalInput.normalWS, positionWS.x);
				o.tSpace1 = float4( normalInput.tangentWS, positionWS.y);
				o.tSpace2 = float4( normalInput.bitangentWS, positionWS.z);

				OUTPUT_LIGHTMAP_UV( v.texcoord1, unity_LightmapST, o.lightmapUVOrVertexSH.xy );
				OUTPUT_SH( normalInput.normalWS.xyz, o.lightmapUVOrVertexSH.xyz );

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					o.lightmapUVOrVertexSH.zw = v.texcoord;
					o.lightmapUVOrVertexSH.xy = v.texcoord * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif

				half3 vertexLight = VertexLighting( positionWS, normalInput.normalWS );
				#ifdef ASE_FOG
					half fogFactor = ComputeFogFactor( positionCS.z );
				#else
					half fogFactor = 0;
				#endif
				o.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
				
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				
				o.clipPos = positionCS;
				#if defined(ASE_NEEDS_FRAG_SCREEN_POSITION)
				o.screenPos = ComputeScreenPos(positionCS);
				#endif
				return o;
			}
			
			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_tangent = v.ase_tangent;
				o.texcoord = v.texcoord;
				o.texcoord1 = v.texcoord1;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				o.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE)
				#define ASE_SV_DEPTH SV_DepthLessEqual  
			#else
				#define ASE_SV_DEPTH SV_Depth
			#endif

			half4 frag ( VertexOutput IN 
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float2 sampleCoords = (IN.lightmapUVOrVertexSH.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					float3 WorldNormal = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					float3 WorldTangent = -cross(GetObjectToWorldMatrix()._13_23_33, WorldNormal);
					float3 WorldBiTangent = cross(WorldNormal, -WorldTangent);
				#else
					float3 WorldNormal = normalize( IN.tSpace0.xyz );
					float3 WorldTangent = IN.tSpace1.xyz;
					float3 WorldBiTangent = IN.tSpace2.xyz;
				#endif
				float3 WorldPosition = float3(IN.tSpace0.w,IN.tSpace1.w,IN.tSpace2.w);
				float3 WorldViewDirection = _WorldSpaceCameraPos.xyz  - WorldPosition;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				#if defined(ASE_NEEDS_FRAG_SCREEN_POSITION)
				float4 ScreenPos = IN.screenPos;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = IN.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#endif
	
				WorldViewDirection = SafeNormalize( WorldViewDirection );

				float2 uv_LeafTex = IN.ase_texcoord7.xy * _LeafTex_ST.xy + _LeafTex_ST.zw;
				float2 uv_TunkTex = IN.ase_texcoord7.xy * _TunkTex_ST.xy + _TunkTex_ST.zw;
				float4 temp_output_551_0 = ( IN.ase_color.b > 0.5 ? tex2D( _LeafTex, uv_LeafTex ) : tex2D( _TunkTex, uv_TunkTex ) );
				float3 temp_output_51_0 = ( WorldPosition * 0.5 );
				float simplePerlin3D57 = snoise( temp_output_51_0*_ColourNoiseSmallScale );
				simplePerlin3D57 = simplePerlin3D57*0.5 + 0.5;
				float3 temp_cast_0 = (simplePerlin3D57).xxx;
				float3 lerpResult60 = lerp( temp_cast_0 , sin( temp_output_51_0 ) , float3( 0,0,0 ));
				float grayscale66 = Luminance(lerpResult60);
				float4 lerpResult71 = lerp( _LeafNoiseColour , _LeafBaseColour , grayscale66);
				float4 lerpResult508 = lerp( _LeafNoiseLargeColour , float4( 0,0,0,0 ) , float4( 0,0,0,0 ));
				float3 temp_output_502_0 = ( WorldPosition * 0.5 );
				float simplePerlin3D500 = snoise( temp_output_502_0*(0.0 + (_ColourNoiseLargeScale - 0.0) * (0.2 - 0.0) / (1.0 - 0.0)) );
				simplePerlin3D500 = simplePerlin3D500*0.5 + 0.5;
				float3 temp_cast_1 = (simplePerlin3D500).xxx;
				float3 lerpResult499 = lerp( temp_cast_1 , sin( temp_output_502_0 ) , float3( 0,0,0 ));
				float grayscale498 = Luminance(lerpResult499);
				float4 lerpResult497 = lerp( lerpResult71 , lerpResult508 , grayscale498);
				float4 blendOpSrc73 = temp_output_551_0;
				float4 blendOpDest73 = lerpResult497;
				float2 temp_output_362_0 = (WorldPosition).xz;
				float simplePerlin2D368 = snoise( temp_output_362_0*0.05 );
				simplePerlin2D368 = simplePerlin2D368*0.5 + 0.5;
				float cos361 = cos( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin361 = sin( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator361 = mul( temp_output_362_0 - float2( 0,0 ) , float2x2( cos361 , -sin361 , sin361 , cos361 )) + float2( 0,0 );
				float grayscale346 = Luminance(float3( rotator361 ,  0.0 ));
				float2 temp_cast_3 = (( sin( _TimeParameters.x * 0.25 ) * (0.0 + (_GustLargeFreq - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) )).xx;
				float temp_output_177_0 = ( (0.0 + (_GustAmount - 0.0) * (4.0 - 0.0) / (1.0 - 0.0)) * ( ( ( simplePerlin2D368 * sin( ( ( grayscale346 * _GustScale ) + ( _TimeParameters.x * -(0.0 + (_GustSmallFreq - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) ) ) ) ) + 0.5 ) / 2.0 ) * ( ( tex2D( _GustNoiseMap, temp_cast_3 ).r + 0.5 ) / 2.0 ) );
				float gustValue373 = temp_output_177_0;
				float4 lerpResult378 = lerp( ( _GustHighlight * gustValue373 ) , float4( 0,0,0,0 ) , ( 1.0 - IN.ase_color.g ));
				float4 temp_output_379_0 = ( (( _LeafFlatColourSwitch )?( lerpResult497 ):( max( blendOpSrc73, blendOpDest73 ) )) + lerpResult378 );
				float4 lerpResult461 = lerp( temp_output_379_0 , _FrostingColour , saturate( ( pow( WorldNormal.y , _FrostingHeight ) * _FrostingFalloff ) ));
				float4 lerpResult570 = lerp( _TrunkNoiseColour , _TrunkBaseColour , grayscale66);
				float4 blendOpSrc575 = temp_output_551_0;
				float4 blendOpDest575 = lerpResult570;
				float4 Albedo78 = ( IN.ase_color.b > 0.5 ? (( _FrostingSwitch )?( lerpResult461 ):( temp_output_379_0 )) : (( _TrunkFlatColourSwitch )?( lerpResult570 ):( max( blendOpSrc575, blendOpDest575 ) )) );
				
				float2 uv_LeafNormalMap = IN.ase_texcoord7.xy * _LeafNormalMap_ST.xy + _LeafNormalMap_ST.zw;
				float3 unpack81 = UnpackNormalScale( tex2D( _LeafNormalMap, uv_LeafNormalMap ), _LeafNormalAmount );
				unpack81.z = lerp( 1, unpack81.z, saturate(_LeafNormalAmount) );
				float2 uv_TrunkNormalMap = IN.ase_texcoord7.xy * _TrunkNormalMap_ST.xy + _TrunkNormalMap_ST.zw;
				float3 unpack562 = UnpackNormalScale( tex2D( _TrunkNormalMap, uv_TrunkNormalMap ), _TrunkNormalAmount );
				unpack562.z = lerp( 1, unpack562.z, saturate(_TrunkNormalAmount) );
				
				float2 uv_EmissiveMask = IN.ase_texcoord7.xy * _EmissiveMask_ST.xy + _EmissiveMask_ST.zw;
				float4 lerpResult417 = lerp( _EmissiveColour , float4( 0,0,0,0 ) , ( 1.0 - tex2D( _EmissiveMask, uv_EmissiveMask ).r ));
				float2 uv_Emissive2Mask = IN.ase_texcoord7.xy * _Emissive2Mask_ST.xy + _Emissive2Mask_ST.zw;
				float4 lerpResult457 = lerp( _Emissive2Colour , float4( 0,0,0,0 ) , ( 1.0 - tex2D( _Emissive2Mask, uv_Emissive2Mask ).r ));
				float2 uv_TrunkEmissiveMask = IN.ase_texcoord7.xy * _TrunkEmissiveMask_ST.xy + _TrunkEmissiveMask_ST.zw;
				float4 lerpResult569 = lerp( _TrunkEmissiveColour , float4( 0,0,0,0 ) , ( 1.0 - tex2D( _TrunkEmissiveMask, uv_TrunkEmissiveMask ).r ));
				float2 temp_cast_5 = (0.03).xx;
				float mulTime436 = _TimeParameters.x * 0.04;
				float2 temp_cast_6 = (mulTime436).xx;
				float2 texCoord435 = IN.ase_texcoord7.xy * temp_cast_5 + temp_cast_6;
				float4 appendResult441 = (float4(WorldPosition.x , 0.0 , WorldPosition.z , 0.0));
				float mulTime430 = _TimeParameters.x * 4.0;
				float grayscale432 = Luminance(sin( ( ( appendResult441 * 0.5 ) + mulTime430 ) ).xyz);
				float clampResult444 = clamp( max( tex2D( _EmissivePulseMap, texCoord435 ).r , grayscale432 ) , 0.0 , 1.0 );
				float4 Emissive420 = ( ( IN.ase_color.b > 0.5 ? ( lerpResult417 + lerpResult457 ) : lerpResult569 ) * _EmissiveAmount * (0.5 + (clampResult444 - 0.0) * (1.0 - 0.5) / (1.0 - 0.0)) );
				
				float temp_output_189_0 = ( 1.0 - IN.ase_color.b );
				float lerpResult188 = lerp( _LeafMetallic , 0.0 , temp_output_189_0);
				float lerpResult195 = lerp( _TrunkMetallic , 0.0 , IN.ase_color.b);
				
				float lerpResult191 = lerp( _LeafSmoothness , 0.0 , temp_output_189_0);
				float lerpResult190 = lerp( _TrunkSmoothness , 0.0 , IN.ase_color.b);
				
				float2 uv_LeafAmbientOcclusion = IN.ase_texcoord7.xy * _LeafAmbientOcclusion_ST.xy + _LeafAmbientOcclusion_ST.zw;
				float2 uv_TrunkAmbientOcclusion = IN.ase_texcoord7.xy * _TrunkAmbientOcclusion_ST.xy + _TrunkAmbientOcclusion_ST.zw;
				
				float Alpha77 = temp_output_551_0.a;
				float4 ase_screenPosNorm = ScreenPos / ScreenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float dither535 = DitherNoiseTex(ase_screenPosNorm, _LOD_DitheringMap, _LOD_DitheringMap_TexelSize);
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch533 = 1.0;
				#else
				float staticSwitch533 = ( unity_LODFade.x < 0.0 ? (0.0 + (sqrt( ( 1.0 - abs( unity_LODFade.x ) ) ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch532 = 1.0;
				#else
				float staticSwitch532 = ( unity_LODFade.x > 0.0 ? (0.0 + (sqrt( unity_LODFade.x ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				dither535 = step( dither535, ( staticSwitch533 * staticSwitch532 ) );
				float LOD_Dithering411 = dither535;
				
				float3 Albedo = Albedo78.rgb;
				float3 Normal = ( IN.ase_color.b > 0.5 ? unpack81 : unpack562 );
				float3 Emission = Emissive420.rgb;
				float3 Specular = 0.5;
				float Metallic = ( lerpResult188 + lerpResult195 );
				float Smoothness = ( lerpResult191 + lerpResult190 );
				float Occlusion = ( IN.ase_color.b > 0.5 ? tex2D( _LeafAmbientOcclusion, uv_LeafAmbientOcclusion ) : tex2D( _TrunkAmbientOcclusion, uv_TrunkAmbientOcclusion ) ).r;
				float Alpha = ( Alpha77 * LOD_Dithering411 );
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;
				#ifdef ASE_DEPTH_WRITE_ON
				float DepthValue = 0;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = WorldPosition;
				inputData.positionCS = IN.clipPos;
				// Forward+ selects punctual lights from the fragment's screen-space cluster.
				inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.clipPos);
				inputData.shadowMask = SAMPLE_SHADOWMASK(IN.lightmapUVOrVertexSH.xy);
				inputData.viewDirectionWS = WorldViewDirection;
				inputData.shadowCoord = ShadowCoords;

				#ifdef _NORMALMAP
					#if _NORMAL_DROPOFF_TS
					inputData.normalWS = TransformTangentToWorld(Normal, half3x3( WorldTangent, WorldBiTangent, WorldNormal ));
					#elif _NORMAL_DROPOFF_OS
					inputData.normalWS = TransformObjectToWorldNormal(Normal);
					#elif _NORMAL_DROPOFF_WS
					inputData.normalWS = Normal;
					#endif
					inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				#else
					inputData.normalWS = WorldNormal;
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = IN.fogFactorAndVertexLight.x;
				#endif

				inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = IN.lightmapUVOrVertexSH.xyz;
				#endif

				inputData.bakedGI = SAMPLE_GI( IN.lightmapUVOrVertexSH.xy, SH, inputData.normalWS );
				#ifdef _ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif
				half4 color = UniversalFragmentPBR(
					inputData, 
					Albedo, 
					Metallic, 
					Specular, 
					Smoothness, 
					Occlusion, 
					Emission, 
					Alpha);

				#ifdef _TRANSMISSION_ASE
				{
					float shadow = _TransmissionShadow;

					Light mainLight = GetMainLight( inputData.shadowCoord );
					float3 mainAtten = mainLight.color * mainLight.distanceAttenuation;
					mainAtten = lerp( mainAtten, mainAtten * mainLight.shadowAttenuation, shadow );
					half3 mainTransmission = max(0 , -dot(inputData.normalWS, mainLight.direction)) * mainAtten * Transmission;
					color.rgb += Albedo * mainTransmission;

					#ifdef _ADDITIONAL_LIGHTS
						int transPixelLightCount = GetAdditionalLightsCount();
						for (int i = 0; i < transPixelLightCount; ++i)
						{
							Light light = GetAdditionalLight(i, inputData.positionWS);
							float3 atten = light.color * light.distanceAttenuation;
							atten = lerp( atten, atten * light.shadowAttenuation, shadow );

							half3 transmission = max(0 , -dot(inputData.normalWS, light.direction)) * atten * Transmission;
							color.rgb += Albedo * transmission;
						}
					#endif
				}
				#endif

				#ifdef _TRANSLUCENCY_ASE
				{
					float shadow = _TransShadow;
					float normal = _TransNormal;
					float scattering = _TransScattering;
					float direct = _TransDirect;
					float ambient = _TransAmbient;
					float strength = _TransStrength;

					Light mainLight = GetMainLight( inputData.shadowCoord );
					float3 mainAtten = mainLight.color * mainLight.distanceAttenuation;
					mainAtten = lerp( mainAtten, mainAtten * mainLight.shadowAttenuation, shadow );

					half3 mainLightDir = mainLight.direction + inputData.normalWS * normal;
					half mainVdotL = pow( saturate( dot( inputData.viewDirectionWS, -mainLightDir ) ), scattering );
					half3 mainTranslucency = mainAtten * ( mainVdotL * direct + inputData.bakedGI * ambient ) * Translucency;
					color.rgb += Albedo * mainTranslucency * strength;

					#ifdef _ADDITIONAL_LIGHTS
						int transPixelLightCount = GetAdditionalLightsCount();
						for (int i = 0; i < transPixelLightCount; ++i)
						{
							Light light = GetAdditionalLight(i, inputData.positionWS);
							float3 atten = light.color * light.distanceAttenuation;
							atten = lerp( atten, atten * light.shadowAttenuation, shadow );

							half3 lightDir = light.direction + inputData.normalWS * normal;
							half VdotL = pow( saturate( dot( inputData.viewDirectionWS, -lightDir ) ), scattering );
							half3 translucency = atten * ( VdotL * direct + inputData.bakedGI * ambient ) * Translucency;
							color.rgb += Albedo * translucency * strength;
						}
					#endif
				}
				#endif

				#ifdef _REFRACTION_ASE
					float4 projScreenPos = ScreenPos / ScreenPos.w;
					float3 refractionOffset = ( RefractionIndex - 1.0 ) * mul( UNITY_MATRIX_V, float4( WorldNormal, 0 ) ).xyz * ( 1.0 - dot( WorldNormal, WorldViewDirection ) );
					projScreenPos.xy += refractionOffset.xy;
					float3 refraction = SHADERGRAPH_SAMPLE_SCENE_COLOR( projScreenPos.xy ) * RefractionColor;
					color.rgb = lerp( refraction, color.rgb, color.a );
					color.a = 1;
				#endif

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						color.rgb = MixFogColor(color.rgb, half3( 0, 0, 0 ), IN.fogFactorAndVertexLight.x );
					#else
						color.rgb = MixFog(color.rgb, IN.fogFactorAndVertexLight.x);
					#endif
				#endif
				
				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				return color;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On
			ZTest LEqual
			AlphaToMask Off

			HLSLPROGRAM
			
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 70701

			
			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_SHADOWCASTER

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local LOD_FADE_CROSSFADE


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FrostingColour;
			float4 _LeafAmbientOcclusion_ST;
			float4 _TrunkEmissiveMask_ST;
			float4 _TrunkEmissiveColour;
			float4 _Emissive2Mask_ST;
			float4 _Emissive2Colour;
			float4 _EmissiveMask_ST;
			float4 _EmissiveColour;
			float4 _TrunkNormalMap_ST;
			float4 _LeafNormalMap_ST;
			float4 _TrunkBaseColour;
			float4 _TrunkNoiseColour;
			float4 _TrunkAmbientOcclusion_ST;
			float4 _GustHighlight;
			float4 _LeafNoiseLargeColour;
			float4 _LeafBaseColour;
			float4 _LOD_DitheringMap_TexelSize;
			float4 _TunkTex_ST;
			float4 _LeafTex_ST;
			float4 _LeafNoiseColour;
			float _JitterFreq;
			float _TrunkSmoothness;
			float _LeafSmoothness;
			float _TrunkMetallic;
			float _LeafMetallic;
			float _EmissiveAmount;
			float _LeafBigNoiseAmount;
			float _LeafBigNoiseScale;
			float _WindDirection;
			float _GustAmount;
			float _GustScale;
			float _GustSmallFreq;
			float _TrunkNormalAmount;
			float _LeafNormalAmount;
			float _WindBaseline;
			float _TotalWindAmount;
			float _Y_multiplier;
			float _TrunkFlatColourSwitch;
			float _FrostingFalloff;
			float _FrostingHeight;
			float _FrostingSwitch;
			float _LeafFlatColourSwitch;
			float _ColourNoiseLargeScale;
			float _ColourNoiseSmallScale;
			float _GustLargeFreq;
			float _JitterAmount;
			#ifdef _TRANSMISSION_ASE
				float _TransmissionShadow;
			#endif
			#ifdef _TRANSLUCENCY_ASE
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _Leaves_NoiseTexture2;
			sampler2D _GustNoiseMap;
			sampler2D _LeafTex;
			sampler2D _TunkTex;
			sampler2D _LOD_DitheringMap;


			float4 CalculateContrast( float contrastValue, float4 colorTarget )
			{
				float t = 0.5 * ( 1.0 - contrastValue );
				return mul( float4x4( contrastValue,0,0,t, 0,contrastValue,0,t, 0,0,contrastValue,t, 0,0,0,1 ), colorTarget );
			}
			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			inline float DitherNoiseTex( float4 screenPos, sampler2D noiseTexture, float4 noiseTexelSize )
			{
				float dither = tex2Dlod( noiseTexture, float4(screenPos.xy * _ScreenParams.xy * noiseTexelSize.xy, 0, 0) ).g;
				float ditherRate = noiseTexelSize.x * noiseTexelSize.y;
				dither = ( 1 - ditherRate ) * dither + ditherRate;
				return dither;
			}
			

			float3 _LightDirection;

			VertexOutput VertexFunction( VertexInput v )
			{
				VertexOutput o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				float mulTime28 = _TimeParameters.x * (0.0 + (_JitterFreq - 0.0) * (0.06 - 0.0) / (1.0 - 0.0));
				float2 temp_cast_0 = (( v.vertex.xyz.x + mulTime28 )).xx;
				float lerpResult50 = lerp( tex2Dlod( _Leaves_NoiseTexture2, float4( temp_cast_0, 0, 0.0) ).r , 0.0 , ( 1.0 - v.ase_color.g ));
				float3 appendResult55 = (float3(lerpResult50 , 0.0 , 0.0));
				float4 Jitter68 = CalculateContrast((0.0 + (_JitterAmount - 0.0) * (0.3 - 0.0) / (1.0 - 0.0)),float4( (appendResult55).xz, 0.0 , 0.0 ));
				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float grayscale304 = Luminance(ase_worldPos);
				float3 temp_cast_2 = (grayscale304).xxx;
				float simplePerlin3D117 = snoise( temp_cast_2 );
				simplePerlin3D117 = simplePerlin3D117*0.5 + 0.5;
				float lerpResult130 = lerp( ( simplePerlin3D117 * sin( ( _LeafBigNoiseScale * ( grayscale304 + _TimeParameters.x ) ) ) ) , 0.0 , ( 1.0 - v.ase_color.g ));
				float cos103 = cos( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin103 = sin( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator103 = mul( float2( 1,1 ) - float2( 0,0 ) , float2x2( cos103 , -sin103 , sin103 , cos103 )) + float2( 0,0 );
				float2 temp_output_362_0 = (ase_worldPos).xz;
				float simplePerlin2D368 = snoise( temp_output_362_0*0.05 );
				simplePerlin2D368 = simplePerlin2D368*0.5 + 0.5;
				float cos361 = cos( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin361 = sin( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator361 = mul( temp_output_362_0 - float2( 0,0 ) , float2x2( cos361 , -sin361 , sin361 , cos361 )) + float2( 0,0 );
				float grayscale346 = Luminance(float3( rotator361 ,  0.0 ));
				float2 temp_cast_4 = (( sin( _TimeParameters.x * 0.25 ) * (0.0 + (_GustLargeFreq - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) )).xx;
				float temp_output_177_0 = ( (0.0 + (_GustAmount - 0.0) * (4.0 - 0.0) / (1.0 - 0.0)) * ( ( ( simplePerlin2D368 * sin( ( ( grayscale346 * _GustScale ) + ( _TimeParameters.x * -(0.0 + (_GustSmallFreq - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) ) ) ) ) + 0.5 ) / 2.0 ) * ( ( tex2Dlod( _GustNoiseMap, float4( temp_cast_4, 0, 0.0) ).r + 0.5 ) / 2.0 ) );
				float lerpResult101 = lerp( ( temp_output_177_0 + _WindBaseline ) , 0.0 , ( 1.0 - v.ase_color.r ));
				float2 break99 = ( rotator103 * lerpResult101 );
				float3 appendResult100 = (float3(break99.x , 0.0 , break99.y));
				float temp_output_293_0 = (0.0 + (_TotalWindAmount - 0.0) * (2.0 - 0.0) / (1.0 - 0.0));
				float3 break169 = ( ( _LeafBigNoiseAmount * lerpResult130 ) + ( appendResult100 * temp_output_293_0 ) );
				float smoothstepResult167 = smoothstep( 0.0 , 2.0 , abs( lerpResult101 ));
				float3 appendResult168 = (float3(break169.x , ( smoothstepResult167 * _Y_multiplier * temp_output_293_0 ) , break169.z));
				float3 worldToObjDir239 = mul( GetWorldToObjectMatrix(), float4( appendResult168, 0 ) ).xyz;
				float3 Wind201 = worldToObjDir239;
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord3 = screenPos;
				
				o.ase_color = v.ase_color;
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( Jitter68 + float4( Wind201 , 0.0 ) ).rgb;
				vertexValue += mul(GetWorldToObjectMatrix(), float4(RpgGrassOffset(ase_worldPos, v.ase_color.g), 0)).xyz;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif
				float3 normalWS = TransformObjectToWorldDir(v.ase_normal);

				float4 clipPos = TransformWorldToHClip( ApplyShadowBias( positionWS, normalWS, _LightDirection ) );

				#if UNITY_REVERSED_Z
					clipPos.z = min(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
				#else
					clipPos.z = max(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				o.clipPos = clipPos;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_color = v.ase_color;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE)
				#define ASE_SV_DEPTH SV_DepthLessEqual  
			#else
				#define ASE_SV_DEPTH SV_Depth
			#endif

			half4 frag(	VertexOutput IN 
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );
				
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv_LeafTex = IN.ase_texcoord2.xy * _LeafTex_ST.xy + _LeafTex_ST.zw;
				float2 uv_TunkTex = IN.ase_texcoord2.xy * _TunkTex_ST.xy + _TunkTex_ST.zw;
				float4 temp_output_551_0 = ( IN.ase_color.b > 0.5 ? tex2D( _LeafTex, uv_LeafTex ) : tex2D( _TunkTex, uv_TunkTex ) );
				float Alpha77 = temp_output_551_0.a;
				float4 screenPos = IN.ase_texcoord3;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float dither535 = DitherNoiseTex(ase_screenPosNorm, _LOD_DitheringMap, _LOD_DitheringMap_TexelSize);
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch533 = 1.0;
				#else
				float staticSwitch533 = ( unity_LODFade.x < 0.0 ? (0.0 + (sqrt( ( 1.0 - abs( unity_LODFade.x ) ) ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch532 = 1.0;
				#else
				float staticSwitch532 = ( unity_LODFade.x > 0.0 ? (0.0 + (sqrt( unity_LODFade.x ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				dither535 = step( dither535, ( staticSwitch533 * staticSwitch532 ) );
				float LOD_Dithering411 = dither535;
				
				float Alpha = ( Alpha77 * LOD_Dithering411 );
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;
				#ifdef ASE_DEPTH_WRITE_ON
				float DepthValue = 0;
				#endif

				#ifdef _ALPHATEST_ON
					#ifdef _ALPHATEST_SHADOW_ON
						clip(Alpha - AlphaClipThresholdShadow);
					#else
						clip(Alpha - AlphaClipThreshold);
					#endif
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif
				return 0;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0
			AlphaToMask Off

			HLSLPROGRAM
			
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 70701

			
			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local LOD_FADE_CROSSFADE


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FrostingColour;
			float4 _LeafAmbientOcclusion_ST;
			float4 _TrunkEmissiveMask_ST;
			float4 _TrunkEmissiveColour;
			float4 _Emissive2Mask_ST;
			float4 _Emissive2Colour;
			float4 _EmissiveMask_ST;
			float4 _EmissiveColour;
			float4 _TrunkNormalMap_ST;
			float4 _LeafNormalMap_ST;
			float4 _TrunkBaseColour;
			float4 _TrunkNoiseColour;
			float4 _TrunkAmbientOcclusion_ST;
			float4 _GustHighlight;
			float4 _LeafNoiseLargeColour;
			float4 _LeafBaseColour;
			float4 _LOD_DitheringMap_TexelSize;
			float4 _TunkTex_ST;
			float4 _LeafTex_ST;
			float4 _LeafNoiseColour;
			float _JitterFreq;
			float _TrunkSmoothness;
			float _LeafSmoothness;
			float _TrunkMetallic;
			float _LeafMetallic;
			float _EmissiveAmount;
			float _LeafBigNoiseAmount;
			float _LeafBigNoiseScale;
			float _WindDirection;
			float _GustAmount;
			float _GustScale;
			float _GustSmallFreq;
			float _TrunkNormalAmount;
			float _LeafNormalAmount;
			float _WindBaseline;
			float _TotalWindAmount;
			float _Y_multiplier;
			float _TrunkFlatColourSwitch;
			float _FrostingFalloff;
			float _FrostingHeight;
			float _FrostingSwitch;
			float _LeafFlatColourSwitch;
			float _ColourNoiseLargeScale;
			float _ColourNoiseSmallScale;
			float _GustLargeFreq;
			float _JitterAmount;
			#ifdef _TRANSMISSION_ASE
				float _TransmissionShadow;
			#endif
			#ifdef _TRANSLUCENCY_ASE
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _Leaves_NoiseTexture2;
			sampler2D _GustNoiseMap;
			sampler2D _LeafTex;
			sampler2D _TunkTex;
			sampler2D _LOD_DitheringMap;


			float4 CalculateContrast( float contrastValue, float4 colorTarget )
			{
				float t = 0.5 * ( 1.0 - contrastValue );
				return mul( float4x4( contrastValue,0,0,t, 0,contrastValue,0,t, 0,0,contrastValue,t, 0,0,0,1 ), colorTarget );
			}
			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			inline float DitherNoiseTex( float4 screenPos, sampler2D noiseTexture, float4 noiseTexelSize )
			{
				float dither = tex2Dlod( noiseTexture, float4(screenPos.xy * _ScreenParams.xy * noiseTexelSize.xy, 0, 0) ).g;
				float ditherRate = noiseTexelSize.x * noiseTexelSize.y;
				dither = ( 1 - ditherRate ) * dither + ditherRate;
				return dither;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float mulTime28 = _TimeParameters.x * (0.0 + (_JitterFreq - 0.0) * (0.06 - 0.0) / (1.0 - 0.0));
				float2 temp_cast_0 = (( v.vertex.xyz.x + mulTime28 )).xx;
				float lerpResult50 = lerp( tex2Dlod( _Leaves_NoiseTexture2, float4( temp_cast_0, 0, 0.0) ).r , 0.0 , ( 1.0 - v.ase_color.g ));
				float3 appendResult55 = (float3(lerpResult50 , 0.0 , 0.0));
				float4 Jitter68 = CalculateContrast((0.0 + (_JitterAmount - 0.0) * (0.3 - 0.0) / (1.0 - 0.0)),float4( (appendResult55).xz, 0.0 , 0.0 ));
				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float grayscale304 = Luminance(ase_worldPos);
				float3 temp_cast_2 = (grayscale304).xxx;
				float simplePerlin3D117 = snoise( temp_cast_2 );
				simplePerlin3D117 = simplePerlin3D117*0.5 + 0.5;
				float lerpResult130 = lerp( ( simplePerlin3D117 * sin( ( _LeafBigNoiseScale * ( grayscale304 + _TimeParameters.x ) ) ) ) , 0.0 , ( 1.0 - v.ase_color.g ));
				float cos103 = cos( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin103 = sin( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator103 = mul( float2( 1,1 ) - float2( 0,0 ) , float2x2( cos103 , -sin103 , sin103 , cos103 )) + float2( 0,0 );
				float2 temp_output_362_0 = (ase_worldPos).xz;
				float simplePerlin2D368 = snoise( temp_output_362_0*0.05 );
				simplePerlin2D368 = simplePerlin2D368*0.5 + 0.5;
				float cos361 = cos( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin361 = sin( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator361 = mul( temp_output_362_0 - float2( 0,0 ) , float2x2( cos361 , -sin361 , sin361 , cos361 )) + float2( 0,0 );
				float grayscale346 = Luminance(float3( rotator361 ,  0.0 ));
				float2 temp_cast_4 = (( sin( _TimeParameters.x * 0.25 ) * (0.0 + (_GustLargeFreq - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) )).xx;
				float temp_output_177_0 = ( (0.0 + (_GustAmount - 0.0) * (4.0 - 0.0) / (1.0 - 0.0)) * ( ( ( simplePerlin2D368 * sin( ( ( grayscale346 * _GustScale ) + ( _TimeParameters.x * -(0.0 + (_GustSmallFreq - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) ) ) ) ) + 0.5 ) / 2.0 ) * ( ( tex2Dlod( _GustNoiseMap, float4( temp_cast_4, 0, 0.0) ).r + 0.5 ) / 2.0 ) );
				float lerpResult101 = lerp( ( temp_output_177_0 + _WindBaseline ) , 0.0 , ( 1.0 - v.ase_color.r ));
				float2 break99 = ( rotator103 * lerpResult101 );
				float3 appendResult100 = (float3(break99.x , 0.0 , break99.y));
				float temp_output_293_0 = (0.0 + (_TotalWindAmount - 0.0) * (2.0 - 0.0) / (1.0 - 0.0));
				float3 break169 = ( ( _LeafBigNoiseAmount * lerpResult130 ) + ( appendResult100 * temp_output_293_0 ) );
				float smoothstepResult167 = smoothstep( 0.0 , 2.0 , abs( lerpResult101 ));
				float3 appendResult168 = (float3(break169.x , ( smoothstepResult167 * _Y_multiplier * temp_output_293_0 ) , break169.z));
				float3 worldToObjDir239 = mul( GetWorldToObjectMatrix(), float4( appendResult168, 0 ) ).xyz;
				float3 Wind201 = worldToObjDir239;
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord3 = screenPos;
				
				o.ase_color = v.ase_color;
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( Jitter68 + float4( Wind201 , 0.0 ) ).rgb;
				vertexValue += mul(GetWorldToObjectMatrix(), float4(RpgGrassOffset(ase_worldPos, v.ase_color.g), 0)).xyz;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;
				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = positionCS;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				o.clipPos = positionCS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_color = v.ase_color;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE)
				#define ASE_SV_DEPTH SV_DepthLessEqual  
			#else
				#define ASE_SV_DEPTH SV_Depth
			#endif
			half4 frag(	VertexOutput IN 
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv_LeafTex = IN.ase_texcoord2.xy * _LeafTex_ST.xy + _LeafTex_ST.zw;
				float2 uv_TunkTex = IN.ase_texcoord2.xy * _TunkTex_ST.xy + _TunkTex_ST.zw;
				float4 temp_output_551_0 = ( IN.ase_color.b > 0.5 ? tex2D( _LeafTex, uv_LeafTex ) : tex2D( _TunkTex, uv_TunkTex ) );
				float Alpha77 = temp_output_551_0.a;
				float4 screenPos = IN.ase_texcoord3;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float dither535 = DitherNoiseTex(ase_screenPosNorm, _LOD_DitheringMap, _LOD_DitheringMap_TexelSize);
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch533 = 1.0;
				#else
				float staticSwitch533 = ( unity_LODFade.x < 0.0 ? (0.0 + (sqrt( ( 1.0 - abs( unity_LODFade.x ) ) ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch532 = 1.0;
				#else
				float staticSwitch532 = ( unity_LODFade.x > 0.0 ? (0.0 + (sqrt( unity_LODFade.x ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				dither535 = step( dither535, ( staticSwitch533 * staticSwitch532 ) );
				float LOD_Dithering411 = dither535;
				
				float Alpha = ( Alpha77 * LOD_Dithering411 );
				float AlphaClipThreshold = 0.5;
				#ifdef ASE_DEPTH_WRITE_ON
				float DepthValue = 0;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				#ifdef ASE_DEPTH_WRITE_ON
				outputDepth = DepthValue;
				#endif
				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM
			
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 70701

			
			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_META

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local LOD_FADE_CROSSFADE


			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FrostingColour;
			float4 _LeafAmbientOcclusion_ST;
			float4 _TrunkEmissiveMask_ST;
			float4 _TrunkEmissiveColour;
			float4 _Emissive2Mask_ST;
			float4 _Emissive2Colour;
			float4 _EmissiveMask_ST;
			float4 _EmissiveColour;
			float4 _TrunkNormalMap_ST;
			float4 _LeafNormalMap_ST;
			float4 _TrunkBaseColour;
			float4 _TrunkNoiseColour;
			float4 _TrunkAmbientOcclusion_ST;
			float4 _GustHighlight;
			float4 _LeafNoiseLargeColour;
			float4 _LeafBaseColour;
			float4 _LOD_DitheringMap_TexelSize;
			float4 _TunkTex_ST;
			float4 _LeafTex_ST;
			float4 _LeafNoiseColour;
			float _JitterFreq;
			float _TrunkSmoothness;
			float _LeafSmoothness;
			float _TrunkMetallic;
			float _LeafMetallic;
			float _EmissiveAmount;
			float _LeafBigNoiseAmount;
			float _LeafBigNoiseScale;
			float _WindDirection;
			float _GustAmount;
			float _GustScale;
			float _GustSmallFreq;
			float _TrunkNormalAmount;
			float _LeafNormalAmount;
			float _WindBaseline;
			float _TotalWindAmount;
			float _Y_multiplier;
			float _TrunkFlatColourSwitch;
			float _FrostingFalloff;
			float _FrostingHeight;
			float _FrostingSwitch;
			float _LeafFlatColourSwitch;
			float _ColourNoiseLargeScale;
			float _ColourNoiseSmallScale;
			float _GustLargeFreq;
			float _JitterAmount;
			#ifdef _TRANSMISSION_ASE
				float _TransmissionShadow;
			#endif
			#ifdef _TRANSLUCENCY_ASE
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _Leaves_NoiseTexture2;
			sampler2D _GustNoiseMap;
			sampler2D _LeafTex;
			sampler2D _TunkTex;
			sampler2D _EmissiveMask;
			sampler2D _Emissive2Mask;
			sampler2D _TrunkEmissiveMask;
			sampler2D _EmissivePulseMap;
			sampler2D _LOD_DitheringMap;


			float4 CalculateContrast( float contrastValue, float4 colorTarget )
			{
				float t = 0.5 * ( 1.0 - contrastValue );
				return mul( float4x4( contrastValue,0,0,t, 0,contrastValue,0,t, 0,0,contrastValue,t, 0,0,0,1 ), colorTarget );
			}
			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			inline float DitherNoiseTex( float4 screenPos, sampler2D noiseTexture, float4 noiseTexelSize )
			{
				float dither = tex2Dlod( noiseTexture, float4(screenPos.xy * _ScreenParams.xy * noiseTexelSize.xy, 0, 0) ).g;
				float ditherRate = noiseTexelSize.x * noiseTexelSize.y;
				dither = ( 1 - ditherRate ) * dither + ditherRate;
				return dither;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float mulTime28 = _TimeParameters.x * (0.0 + (_JitterFreq - 0.0) * (0.06 - 0.0) / (1.0 - 0.0));
				float2 temp_cast_0 = (( v.vertex.xyz.x + mulTime28 )).xx;
				float lerpResult50 = lerp( tex2Dlod( _Leaves_NoiseTexture2, float4( temp_cast_0, 0, 0.0) ).r , 0.0 , ( 1.0 - v.ase_color.g ));
				float3 appendResult55 = (float3(lerpResult50 , 0.0 , 0.0));
				float4 Jitter68 = CalculateContrast((0.0 + (_JitterAmount - 0.0) * (0.3 - 0.0) / (1.0 - 0.0)),float4( (appendResult55).xz, 0.0 , 0.0 ));
				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float grayscale304 = Luminance(ase_worldPos);
				float3 temp_cast_2 = (grayscale304).xxx;
				float simplePerlin3D117 = snoise( temp_cast_2 );
				simplePerlin3D117 = simplePerlin3D117*0.5 + 0.5;
				float lerpResult130 = lerp( ( simplePerlin3D117 * sin( ( _LeafBigNoiseScale * ( grayscale304 + _TimeParameters.x ) ) ) ) , 0.0 , ( 1.0 - v.ase_color.g ));
				float cos103 = cos( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin103 = sin( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator103 = mul( float2( 1,1 ) - float2( 0,0 ) , float2x2( cos103 , -sin103 , sin103 , cos103 )) + float2( 0,0 );
				float2 temp_output_362_0 = (ase_worldPos).xz;
				float simplePerlin2D368 = snoise( temp_output_362_0*0.05 );
				simplePerlin2D368 = simplePerlin2D368*0.5 + 0.5;
				float cos361 = cos( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin361 = sin( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator361 = mul( temp_output_362_0 - float2( 0,0 ) , float2x2( cos361 , -sin361 , sin361 , cos361 )) + float2( 0,0 );
				float grayscale346 = Luminance(float3( rotator361 ,  0.0 ));
				float2 temp_cast_4 = (( sin( _TimeParameters.x * 0.25 ) * (0.0 + (_GustLargeFreq - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) )).xx;
				float temp_output_177_0 = ( (0.0 + (_GustAmount - 0.0) * (4.0 - 0.0) / (1.0 - 0.0)) * ( ( ( simplePerlin2D368 * sin( ( ( grayscale346 * _GustScale ) + ( _TimeParameters.x * -(0.0 + (_GustSmallFreq - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) ) ) ) ) + 0.5 ) / 2.0 ) * ( ( tex2Dlod( _GustNoiseMap, float4( temp_cast_4, 0, 0.0) ).r + 0.5 ) / 2.0 ) );
				float lerpResult101 = lerp( ( temp_output_177_0 + _WindBaseline ) , 0.0 , ( 1.0 - v.ase_color.r ));
				float2 break99 = ( rotator103 * lerpResult101 );
				float3 appendResult100 = (float3(break99.x , 0.0 , break99.y));
				float temp_output_293_0 = (0.0 + (_TotalWindAmount - 0.0) * (2.0 - 0.0) / (1.0 - 0.0));
				float3 break169 = ( ( _LeafBigNoiseAmount * lerpResult130 ) + ( appendResult100 * temp_output_293_0 ) );
				float smoothstepResult167 = smoothstep( 0.0 , 2.0 , abs( lerpResult101 ));
				float3 appendResult168 = (float3(break169.x , ( smoothstepResult167 * _Y_multiplier * temp_output_293_0 ) , break169.z));
				float3 worldToObjDir239 = mul( GetWorldToObjectMatrix(), float4( appendResult168, 0 ) ).xyz;
				float3 Wind201 = worldToObjDir239;
				
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				o.ase_texcoord3.xyz = ase_worldNormal;
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord4 = screenPos;
				
				o.ase_color = v.ase_color;
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.zw = 0;
				o.ase_texcoord3.w = 0;
				
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( Jitter68 + float4( Wind201 , 0.0 ) ).rgb;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				o.clipPos = MetaVertexPosition( v.vertex, v.texcoord1.xy, v.texcoord1.xy, unity_LightmapST, unity_DynamicLightmapST );
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = o.clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.texcoord1 = v.texcoord1;
				o.texcoord2 = v.texcoord2;
				o.ase_color = v.ase_color;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				o.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv_LeafTex = IN.ase_texcoord2.xy * _LeafTex_ST.xy + _LeafTex_ST.zw;
				float2 uv_TunkTex = IN.ase_texcoord2.xy * _TunkTex_ST.xy + _TunkTex_ST.zw;
				float4 temp_output_551_0 = ( IN.ase_color.b > 0.5 ? tex2D( _LeafTex, uv_LeafTex ) : tex2D( _TunkTex, uv_TunkTex ) );
				float3 temp_output_51_0 = ( WorldPosition * 0.5 );
				float simplePerlin3D57 = snoise( temp_output_51_0*_ColourNoiseSmallScale );
				simplePerlin3D57 = simplePerlin3D57*0.5 + 0.5;
				float3 temp_cast_0 = (simplePerlin3D57).xxx;
				float3 lerpResult60 = lerp( temp_cast_0 , sin( temp_output_51_0 ) , float3( 0,0,0 ));
				float grayscale66 = Luminance(lerpResult60);
				float4 lerpResult71 = lerp( _LeafNoiseColour , _LeafBaseColour , grayscale66);
				float4 lerpResult508 = lerp( _LeafNoiseLargeColour , float4( 0,0,0,0 ) , float4( 0,0,0,0 ));
				float3 temp_output_502_0 = ( WorldPosition * 0.5 );
				float simplePerlin3D500 = snoise( temp_output_502_0*(0.0 + (_ColourNoiseLargeScale - 0.0) * (0.2 - 0.0) / (1.0 - 0.0)) );
				simplePerlin3D500 = simplePerlin3D500*0.5 + 0.5;
				float3 temp_cast_1 = (simplePerlin3D500).xxx;
				float3 lerpResult499 = lerp( temp_cast_1 , sin( temp_output_502_0 ) , float3( 0,0,0 ));
				float grayscale498 = Luminance(lerpResult499);
				float4 lerpResult497 = lerp( lerpResult71 , lerpResult508 , grayscale498);
				float4 blendOpSrc73 = temp_output_551_0;
				float4 blendOpDest73 = lerpResult497;
				float2 temp_output_362_0 = (WorldPosition).xz;
				float simplePerlin2D368 = snoise( temp_output_362_0*0.05 );
				simplePerlin2D368 = simplePerlin2D368*0.5 + 0.5;
				float cos361 = cos( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin361 = sin( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator361 = mul( temp_output_362_0 - float2( 0,0 ) , float2x2( cos361 , -sin361 , sin361 , cos361 )) + float2( 0,0 );
				float grayscale346 = Luminance(float3( rotator361 ,  0.0 ));
				float2 temp_cast_3 = (( sin( _TimeParameters.x * 0.25 ) * (0.0 + (_GustLargeFreq - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) )).xx;
				float temp_output_177_0 = ( (0.0 + (_GustAmount - 0.0) * (4.0 - 0.0) / (1.0 - 0.0)) * ( ( ( simplePerlin2D368 * sin( ( ( grayscale346 * _GustScale ) + ( _TimeParameters.x * -(0.0 + (_GustSmallFreq - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) ) ) ) ) + 0.5 ) / 2.0 ) * ( ( tex2D( _GustNoiseMap, temp_cast_3 ).r + 0.5 ) / 2.0 ) );
				float gustValue373 = temp_output_177_0;
				float4 lerpResult378 = lerp( ( _GustHighlight * gustValue373 ) , float4( 0,0,0,0 ) , ( 1.0 - IN.ase_color.g ));
				float4 temp_output_379_0 = ( (( _LeafFlatColourSwitch )?( lerpResult497 ):( max( blendOpSrc73, blendOpDest73 ) )) + lerpResult378 );
				float3 ase_worldNormal = IN.ase_texcoord3.xyz;
				float4 lerpResult461 = lerp( temp_output_379_0 , _FrostingColour , saturate( ( pow( ase_worldNormal.y , _FrostingHeight ) * _FrostingFalloff ) ));
				float4 lerpResult570 = lerp( _TrunkNoiseColour , _TrunkBaseColour , grayscale66);
				float4 blendOpSrc575 = temp_output_551_0;
				float4 blendOpDest575 = lerpResult570;
				float4 Albedo78 = ( IN.ase_color.b > 0.5 ? (( _FrostingSwitch )?( lerpResult461 ):( temp_output_379_0 )) : (( _TrunkFlatColourSwitch )?( lerpResult570 ):( max( blendOpSrc575, blendOpDest575 ) )) );
				
				float2 uv_EmissiveMask = IN.ase_texcoord2.xy * _EmissiveMask_ST.xy + _EmissiveMask_ST.zw;
				float4 lerpResult417 = lerp( _EmissiveColour , float4( 0,0,0,0 ) , ( 1.0 - tex2D( _EmissiveMask, uv_EmissiveMask ).r ));
				float2 uv_Emissive2Mask = IN.ase_texcoord2.xy * _Emissive2Mask_ST.xy + _Emissive2Mask_ST.zw;
				float4 lerpResult457 = lerp( _Emissive2Colour , float4( 0,0,0,0 ) , ( 1.0 - tex2D( _Emissive2Mask, uv_Emissive2Mask ).r ));
				float2 uv_TrunkEmissiveMask = IN.ase_texcoord2.xy * _TrunkEmissiveMask_ST.xy + _TrunkEmissiveMask_ST.zw;
				float4 lerpResult569 = lerp( _TrunkEmissiveColour , float4( 0,0,0,0 ) , ( 1.0 - tex2D( _TrunkEmissiveMask, uv_TrunkEmissiveMask ).r ));
				float2 temp_cast_5 = (0.03).xx;
				float mulTime436 = _TimeParameters.x * 0.04;
				float2 temp_cast_6 = (mulTime436).xx;
				float2 texCoord435 = IN.ase_texcoord2.xy * temp_cast_5 + temp_cast_6;
				float4 appendResult441 = (float4(WorldPosition.x , 0.0 , WorldPosition.z , 0.0));
				float mulTime430 = _TimeParameters.x * 4.0;
				float grayscale432 = Luminance(sin( ( ( appendResult441 * 0.5 ) + mulTime430 ) ).xyz);
				float clampResult444 = clamp( max( tex2D( _EmissivePulseMap, texCoord435 ).r , grayscale432 ) , 0.0 , 1.0 );
				float4 Emissive420 = ( ( IN.ase_color.b > 0.5 ? ( lerpResult417 + lerpResult457 ) : lerpResult569 ) * _EmissiveAmount * (0.5 + (clampResult444 - 0.0) * (1.0 - 0.5) / (1.0 - 0.0)) );
				
				float Alpha77 = temp_output_551_0.a;
				float4 screenPos = IN.ase_texcoord4;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float dither535 = DitherNoiseTex(ase_screenPosNorm, _LOD_DitheringMap, _LOD_DitheringMap_TexelSize);
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch533 = 1.0;
				#else
				float staticSwitch533 = ( unity_LODFade.x < 0.0 ? (0.0 + (sqrt( ( 1.0 - abs( unity_LODFade.x ) ) ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch532 = 1.0;
				#else
				float staticSwitch532 = ( unity_LODFade.x > 0.0 ? (0.0 + (sqrt( unity_LODFade.x ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				dither535 = step( dither535, ( staticSwitch533 * staticSwitch532 ) );
				float LOD_Dithering411 = dither535;
				
				
				float3 Albedo = Albedo78.rgb;
				float3 Emission = Emissive420.rgb;
				float Alpha = ( Alpha77 * LOD_Dithering411 );
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				MetaInput metaInput = (MetaInput)0;
				metaInput.Albedo = Albedo;
				metaInput.Emission = Emission;
				
				return MetaFragment(metaInput);
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM
			
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 70701

			
			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_2D

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local LOD_FADE_CROSSFADE


			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FrostingColour;
			float4 _LeafAmbientOcclusion_ST;
			float4 _TrunkEmissiveMask_ST;
			float4 _TrunkEmissiveColour;
			float4 _Emissive2Mask_ST;
			float4 _Emissive2Colour;
			float4 _EmissiveMask_ST;
			float4 _EmissiveColour;
			float4 _TrunkNormalMap_ST;
			float4 _LeafNormalMap_ST;
			float4 _TrunkBaseColour;
			float4 _TrunkNoiseColour;
			float4 _TrunkAmbientOcclusion_ST;
			float4 _GustHighlight;
			float4 _LeafNoiseLargeColour;
			float4 _LeafBaseColour;
			float4 _LOD_DitheringMap_TexelSize;
			float4 _TunkTex_ST;
			float4 _LeafTex_ST;
			float4 _LeafNoiseColour;
			float _JitterFreq;
			float _TrunkSmoothness;
			float _LeafSmoothness;
			float _TrunkMetallic;
			float _LeafMetallic;
			float _EmissiveAmount;
			float _LeafBigNoiseAmount;
			float _LeafBigNoiseScale;
			float _WindDirection;
			float _GustAmount;
			float _GustScale;
			float _GustSmallFreq;
			float _TrunkNormalAmount;
			float _LeafNormalAmount;
			float _WindBaseline;
			float _TotalWindAmount;
			float _Y_multiplier;
			float _TrunkFlatColourSwitch;
			float _FrostingFalloff;
			float _FrostingHeight;
			float _FrostingSwitch;
			float _LeafFlatColourSwitch;
			float _ColourNoiseLargeScale;
			float _ColourNoiseSmallScale;
			float _GustLargeFreq;
			float _JitterAmount;
			#ifdef _TRANSMISSION_ASE
				float _TransmissionShadow;
			#endif
			#ifdef _TRANSLUCENCY_ASE
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _Leaves_NoiseTexture2;
			sampler2D _GustNoiseMap;
			sampler2D _LeafTex;
			sampler2D _TunkTex;
			sampler2D _LOD_DitheringMap;


			float4 CalculateContrast( float contrastValue, float4 colorTarget )
			{
				float t = 0.5 * ( 1.0 - contrastValue );
				return mul( float4x4( contrastValue,0,0,t, 0,contrastValue,0,t, 0,0,contrastValue,t, 0,0,0,1 ), colorTarget );
			}
			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			inline float DitherNoiseTex( float4 screenPos, sampler2D noiseTexture, float4 noiseTexelSize )
			{
				float dither = tex2Dlod( noiseTexture, float4(screenPos.xy * _ScreenParams.xy * noiseTexelSize.xy, 0, 0) ).g;
				float ditherRate = noiseTexelSize.x * noiseTexelSize.y;
				dither = ( 1 - ditherRate ) * dither + ditherRate;
				return dither;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				float mulTime28 = _TimeParameters.x * (0.0 + (_JitterFreq - 0.0) * (0.06 - 0.0) / (1.0 - 0.0));
				float2 temp_cast_0 = (( v.vertex.xyz.x + mulTime28 )).xx;
				float lerpResult50 = lerp( tex2Dlod( _Leaves_NoiseTexture2, float4( temp_cast_0, 0, 0.0) ).r , 0.0 , ( 1.0 - v.ase_color.g ));
				float3 appendResult55 = (float3(lerpResult50 , 0.0 , 0.0));
				float4 Jitter68 = CalculateContrast((0.0 + (_JitterAmount - 0.0) * (0.3 - 0.0) / (1.0 - 0.0)),float4( (appendResult55).xz, 0.0 , 0.0 ));
				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float grayscale304 = Luminance(ase_worldPos);
				float3 temp_cast_2 = (grayscale304).xxx;
				float simplePerlin3D117 = snoise( temp_cast_2 );
				simplePerlin3D117 = simplePerlin3D117*0.5 + 0.5;
				float lerpResult130 = lerp( ( simplePerlin3D117 * sin( ( _LeafBigNoiseScale * ( grayscale304 + _TimeParameters.x ) ) ) ) , 0.0 , ( 1.0 - v.ase_color.g ));
				float cos103 = cos( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin103 = sin( (0.0 + (_WindDirection - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator103 = mul( float2( 1,1 ) - float2( 0,0 ) , float2x2( cos103 , -sin103 , sin103 , cos103 )) + float2( 0,0 );
				float2 temp_output_362_0 = (ase_worldPos).xz;
				float simplePerlin2D368 = snoise( temp_output_362_0*0.05 );
				simplePerlin2D368 = simplePerlin2D368*0.5 + 0.5;
				float cos361 = cos( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin361 = sin( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator361 = mul( temp_output_362_0 - float2( 0,0 ) , float2x2( cos361 , -sin361 , sin361 , cos361 )) + float2( 0,0 );
				float grayscale346 = Luminance(float3( rotator361 ,  0.0 ));
				float2 temp_cast_4 = (( sin( _TimeParameters.x * 0.25 ) * (0.0 + (_GustLargeFreq - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) )).xx;
				float temp_output_177_0 = ( (0.0 + (_GustAmount - 0.0) * (4.0 - 0.0) / (1.0 - 0.0)) * ( ( ( simplePerlin2D368 * sin( ( ( grayscale346 * _GustScale ) + ( _TimeParameters.x * -(0.0 + (_GustSmallFreq - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) ) ) ) ) + 0.5 ) / 2.0 ) * ( ( tex2Dlod( _GustNoiseMap, float4( temp_cast_4, 0, 0.0) ).r + 0.5 ) / 2.0 ) );
				float lerpResult101 = lerp( ( temp_output_177_0 + _WindBaseline ) , 0.0 , ( 1.0 - v.ase_color.r ));
				float2 break99 = ( rotator103 * lerpResult101 );
				float3 appendResult100 = (float3(break99.x , 0.0 , break99.y));
				float temp_output_293_0 = (0.0 + (_TotalWindAmount - 0.0) * (2.0 - 0.0) / (1.0 - 0.0));
				float3 break169 = ( ( _LeafBigNoiseAmount * lerpResult130 ) + ( appendResult100 * temp_output_293_0 ) );
				float smoothstepResult167 = smoothstep( 0.0 , 2.0 , abs( lerpResult101 ));
				float3 appendResult168 = (float3(break169.x , ( smoothstepResult167 * _Y_multiplier * temp_output_293_0 ) , break169.z));
				float3 worldToObjDir239 = mul( GetWorldToObjectMatrix(), float4( appendResult168, 0 ) ).xyz;
				float3 Wind201 = worldToObjDir239;
				
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				o.ase_texcoord3.xyz = ase_worldNormal;
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord4 = screenPos;
				
				o.ase_color = v.ase_color;
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.zw = 0;
				o.ase_texcoord3.w = 0;
				
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = ( Jitter68 + float4( Wind201 , 0.0 ) ).rgb;
				vertexValue += mul(GetWorldToObjectMatrix(), float4(RpgGrassOffset(ase_worldPos, v.ase_color.g), 0)).xyz;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = positionCS;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				o.clipPos = positionCS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_color = v.ase_color;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv_LeafTex = IN.ase_texcoord2.xy * _LeafTex_ST.xy + _LeafTex_ST.zw;
				float2 uv_TunkTex = IN.ase_texcoord2.xy * _TunkTex_ST.xy + _TunkTex_ST.zw;
				float4 temp_output_551_0 = ( IN.ase_color.b > 0.5 ? tex2D( _LeafTex, uv_LeafTex ) : tex2D( _TunkTex, uv_TunkTex ) );
				float3 temp_output_51_0 = ( WorldPosition * 0.5 );
				float simplePerlin3D57 = snoise( temp_output_51_0*_ColourNoiseSmallScale );
				simplePerlin3D57 = simplePerlin3D57*0.5 + 0.5;
				float3 temp_cast_0 = (simplePerlin3D57).xxx;
				float3 lerpResult60 = lerp( temp_cast_0 , sin( temp_output_51_0 ) , float3( 0,0,0 ));
				float grayscale66 = Luminance(lerpResult60);
				float4 lerpResult71 = lerp( _LeafNoiseColour , _LeafBaseColour , grayscale66);
				float4 lerpResult508 = lerp( _LeafNoiseLargeColour , float4( 0,0,0,0 ) , float4( 0,0,0,0 ));
				float3 temp_output_502_0 = ( WorldPosition * 0.5 );
				float simplePerlin3D500 = snoise( temp_output_502_0*(0.0 + (_ColourNoiseLargeScale - 0.0) * (0.2 - 0.0) / (1.0 - 0.0)) );
				simplePerlin3D500 = simplePerlin3D500*0.5 + 0.5;
				float3 temp_cast_1 = (simplePerlin3D500).xxx;
				float3 lerpResult499 = lerp( temp_cast_1 , sin( temp_output_502_0 ) , float3( 0,0,0 ));
				float grayscale498 = Luminance(lerpResult499);
				float4 lerpResult497 = lerp( lerpResult71 , lerpResult508 , grayscale498);
				float4 blendOpSrc73 = temp_output_551_0;
				float4 blendOpDest73 = lerpResult497;
				float2 temp_output_362_0 = (WorldPosition).xz;
				float simplePerlin2D368 = snoise( temp_output_362_0*0.05 );
				simplePerlin2D368 = simplePerlin2D368*0.5 + 0.5;
				float cos361 = cos( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float sin361 = sin( (0.0 + (( 1.0 - _WindDirection ) - 0.0) * (6.28 - 0.0) / (1.0 - 0.0)) );
				float2 rotator361 = mul( temp_output_362_0 - float2( 0,0 ) , float2x2( cos361 , -sin361 , sin361 , cos361 )) + float2( 0,0 );
				float grayscale346 = Luminance(float3( rotator361 ,  0.0 ));
				float2 temp_cast_3 = (( sin( _TimeParameters.x * 0.25 ) * (0.0 + (_GustLargeFreq - 0.0) * (2.0 - 0.0) / (1.0 - 0.0)) )).xx;
				float temp_output_177_0 = ( (0.0 + (_GustAmount - 0.0) * (4.0 - 0.0) / (1.0 - 0.0)) * ( ( ( simplePerlin2D368 * sin( ( ( grayscale346 * _GustScale ) + ( _TimeParameters.x * -(0.0 + (_GustSmallFreq - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) ) ) ) ) + 0.5 ) / 2.0 ) * ( ( tex2D( _GustNoiseMap, temp_cast_3 ).r + 0.5 ) / 2.0 ) );
				float gustValue373 = temp_output_177_0;
				float4 lerpResult378 = lerp( ( _GustHighlight * gustValue373 ) , float4( 0,0,0,0 ) , ( 1.0 - IN.ase_color.g ));
				float4 temp_output_379_0 = ( (( _LeafFlatColourSwitch )?( lerpResult497 ):( max( blendOpSrc73, blendOpDest73 ) )) + lerpResult378 );
				float3 ase_worldNormal = IN.ase_texcoord3.xyz;
				float4 lerpResult461 = lerp( temp_output_379_0 , _FrostingColour , saturate( ( pow( ase_worldNormal.y , _FrostingHeight ) * _FrostingFalloff ) ));
				float4 lerpResult570 = lerp( _TrunkNoiseColour , _TrunkBaseColour , grayscale66);
				float4 blendOpSrc575 = temp_output_551_0;
				float4 blendOpDest575 = lerpResult570;
				float4 Albedo78 = ( IN.ase_color.b > 0.5 ? (( _FrostingSwitch )?( lerpResult461 ):( temp_output_379_0 )) : (( _TrunkFlatColourSwitch )?( lerpResult570 ):( max( blendOpSrc575, blendOpDest575 ) )) );
				
				float Alpha77 = temp_output_551_0.a;
				float4 screenPos = IN.ase_texcoord4;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float dither535 = DitherNoiseTex(ase_screenPosNorm, _LOD_DitheringMap, _LOD_DitheringMap_TexelSize);
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch533 = 1.0;
				#else
				float staticSwitch533 = ( unity_LODFade.x < 0.0 ? (0.0 + (sqrt( ( 1.0 - abs( unity_LODFade.x ) ) ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				#ifdef LOD_FADE_CROSSFADE
				float staticSwitch532 = 1.0;
				#else
				float staticSwitch532 = ( unity_LODFade.x > 0.0 ? (0.0 + (sqrt( unity_LODFade.x ) - 0.0) * (1.0 - 0.0) / (0.7 - 0.0)) : 1.0 );
				#endif
				dither535 = step( dither535, ( staticSwitch533 * staticSwitch532 ) );
				float LOD_Dithering411 = dither535;
				
				
				float3 Albedo = Albedo78.rgb;
				float Alpha = ( Alpha77 * LOD_Dithering411 );
				float AlphaClipThreshold = 0.5;

				half4 color = half4( Albedo, Alpha );

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				return color;
			}
			ENDHLSL
		}
		
	}
	
	Fallback "Hidden/InternalErrorShader"
	
}
