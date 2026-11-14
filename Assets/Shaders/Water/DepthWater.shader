Shader "RPG/Water/Depth Water"
{
    Properties
    {
        [Header(Depth and shore)]
        _ShallowColor ("Shallow tint", Color) = (0.22, 0.65, 0.57, 1)
        _DeepColor ("Deep tint", Color) = (0.015, 0.12, 0.18, 1)
        _ColorDepth ("Color transition depth (m)", Float) = 4
        _Absorption ("Absorption per metre", Range(0, 5)) = 0.35
        _ShoreBlend ("Shore blend distance (m)", Float) = 0.3
        [Header(Two normal layers)]
        [Normal] _NormalA ("Normal A", 2D) = "bump" {}
        [Normal] _NormalB ("Normal B", 2D) = "bump" {}
        _NormalScale ("Normal strength", Range(0, 2)) = 0.45
        _NormalTiling ("Normal tiling A and B (per metre)", Vector) = (0.3, 0.3, 0.53, 0.53)
        _NormalSpeed ("Normal velocity A and B", Vector) = (0.025, 0.015, -0.018, 0.03)
        [Header(Texture noise and refraction)]
        _NoiseTex ("Noise (RG)", 2D) = "gray" {}
        _NoiseTiling ("Noise tiling (per metre)", Float) = 0.25
        _NoiseSpeed ("Noise velocity", Vector) = (0.02, -0.015, 0, 0)
        _Refraction ("Bottom distortion (screen UV)", Range(0, 0.1)) = 0.012
        _BottomDispersion ("Bottom chromatic separation", Range(0, 1)) = 0.15
        [Header(Surface overlay)]
        _SurfaceTex ("Surface texture (RGBA)", 2D) = "white" {}
        _SurfaceColor ("Surface tint and opacity", Color) = (0.7, 0.85, 0.8, 0)
        _SurfaceTiling ("Surface tiling (per metre)", Float) = 0.3
        _SurfaceSpeed ("Surface velocity", Vector) = (0.01, 0.008, 0, 0)
        _SurfaceDistortion ("Surface texture distortion (UV)", Range(0, 0.5)) = 0.06
        [Header(Caustics)]
        _CausticsTex ("Caustics (R)", 2D) = "black" {}
        _CausticsTiling ("Caustics tiling (per metre)", Float) = 0.6
        _CausticsSpeed ("Caustics velocity", Vector) = (0.04, 0.025, 0, 0)
        _CausticsStrength ("Caustics intensity", Range(0, 8)) = 1.8
        _CausticsDepth ("Caustics fade out depth (m)", Float) = 8
        _CausticsDistortion ("Caustics noise distortion (UV)", Range(0, 0.5)) = 0.08
        _CausticsDispersion ("Caustics chromatic offset (UV)", Range(0, 0.1)) = 0.008
        [Header(Surface light)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.85
        _SpecularStrength ("Sun highlight", Range(0, 4)) = 1
        _GlintRoughness ("Minimum highlight roughness", Range(0.045, 0.5)) = 0.18
        _SunPathStretch ("Low sun path stretch", Range(1, 8)) = 4
        _GlintDetailTiling ("Glint ripple tiling (per metre)", Float) = 3
        _GlintDetailStrength ("Glint ripple normal strength", Range(0, 1)) = 0.16
        _FresnelStrength ("Environment reflection", Range(0, 1)) = 0.65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            // The scene colour is already included in the result. Only the shore
            // coverage belongs in alpha; blending transmission again darkens it twice.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "DepthWater.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
