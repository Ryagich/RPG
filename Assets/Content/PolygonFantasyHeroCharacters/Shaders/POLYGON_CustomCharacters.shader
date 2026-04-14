Shader "Shader Graphs/POLYGON_CustomCharacters"
{
    Properties
    {
        _Color_Primary("Color_Primary", Color) = (0.2431373,0.4196079,0.6196079,0)
        _Color_Secondary("Color_Secondary", Color) = (0.8196079,0.6431373,0.2980392,0)
        _Color_Leather_Primary("Color_Leather_Primary", Color) = (0.282353,0.2078432,0.1647059,0)
        _Color_Metal_Primary("Color_Metal_Primary", Color) = (0.5960785,0.6117647,0.627451,0)
        _Color_Leather_Secondary("Color_Leather_Secondary", Color) = (0.372549,0.3294118,0.2784314,0)
        _Color_Metal_Dark("Color_Metal_Dark", Color) = (0.1764706,0.1960784,0.2156863,0)
        _Color_Metal_Secondary("Color_Metal_Secondary", Color) = (0.345098,0.3764706,0.3960785,0)
        _Color_Hair("Color_Hair", Color) = (0.2627451,0.2117647,0.1333333,0)
        _Color_Skin("Color_Skin", Color) = (1,0.8000001,0.682353,1)
        _Color_Stubble("Color_Stubble", Color) = (0.8039216,0.7019608,0.6313726,1)
        _Color_Scar("Color_Scar", Color) = (0.9294118,0.6862745,0.5921569,1)
        _Color_BodyArt("Color_BodyArt", Color) = (0.2283196,0.5822246,0.7573529,1)
        _Color_Eyes("Color_Eyes", Color) = (0.2283196,0.5822246,0.7573529,1)
        _Texture("Texture", 2D) = "white" {}
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0
        _Emission("Emission", Range(0, 1)) = 0
        _BodyArt_Amount("BodyArt_Amount", Range(0, 1)) = 0
        [HideInInspector]_Mask_02("Mask_02", 2D) = "white" {}
        [HideInInspector]_Mask_05("Mask_05", 2D) = "white" {}
        [HideInInspector]_Mask_03("Mask_03", 2D) = "white" {}
        [HideInInspector]_Mask_04("Mask_04", 2D) = "white" {}
        [HideInInspector]_Mask_01("Mask_01", 2D) = "white" {}
        [HideInInspector]_texcoord("", 2D) = "white" {}
        [HideInInspector] __dirty("", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "SimpleLit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One Zero
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);
            TEXTURE2D(_Mask_01);
            SAMPLER(sampler_Mask_01);
            TEXTURE2D(_Mask_02);
            SAMPLER(sampler_Mask_02);
            TEXTURE2D(_Mask_03);
            SAMPLER(sampler_Mask_03);
            TEXTURE2D(_Mask_04);
            SAMPLER(sampler_Mask_04);
            TEXTURE2D(_Mask_05);
            SAMPLER(sampler_Mask_05);

            CBUFFER_START(UnityPerMaterial)
            float4 _Texture_ST;
            float4 _Mask_01_ST;
            float4 _Mask_02_ST;
            float4 _Mask_03_ST;
            float4 _Mask_04_ST;
            float4 _Mask_05_ST;
            half4 _Color_Primary;
            half4 _Color_Secondary;
            half4 _Color_Leather_Primary;
            half4 _Color_Metal_Primary;
            half4 _Color_Leather_Secondary;
            half4 _Color_Metal_Dark;
            half4 _Color_Metal_Secondary;
            half4 _Color_Hair;
            half4 _Color_Skin;
            half4 _Color_Stubble;
            half4 _Color_Scar;
            half4 _Color_BodyArt;
            half4 _Color_Eyes;
            half _Metallic;
            half _Smoothness;
            half _Emission;
            half _BodyArt_Amount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
            #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD7;
            #endif
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            inline half MaskStep(half channel)
            {
                return step(channel, 0.5h);
            }

            inline half3 EvaluateCharacterAlbedo(float2 uv)
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uv);
                half4 mask01 = SAMPLE_TEXTURE2D(_Mask_01, sampler_Mask_01, uv);
                half4 mask02 = SAMPLE_TEXTURE2D(_Mask_02, sampler_Mask_02, uv);
                half4 mask03 = SAMPLE_TEXTURE2D(_Mask_03, sampler_Mask_03, uv);
                half4 mask04 = SAMPLE_TEXTURE2D(_Mask_04, sampler_Mask_04, uv);
                half4 mask05 = SAMPLE_TEXTURE2D(_Mask_05, sampler_Mask_05, uv);

                half4 color = lerp(baseTex, _Color_Primary, MaskStep(mask01.r));
                color = lerp(color, _Color_Secondary, MaskStep(mask01.g));
                color = lerp(color, _Color_Leather_Primary, MaskStep(mask04.r));
                color = lerp(color, _Color_Leather_Secondary, MaskStep(mask04.g));
                color = lerp(color, _Color_Metal_Primary, MaskStep(mask02.r));
                color = lerp(color, _Color_Metal_Secondary, MaskStep(mask02.g));
                color = lerp(color, _Color_Metal_Dark, MaskStep(mask02.b));
                color = lerp(color, _Color_Hair, MaskStep(mask04.b));
                color = lerp(color, _Color_Skin, MaskStep(mask03.r));
                color = lerp(color, _Color_Stubble, MaskStep(mask03.b));
                color = lerp(color, _Color_Scar, MaskStep(mask03.g));
                color = lerp(_Color_Eyes, color, mask05.r);

                half bodyArtMask = lerp(mask01.b, 1.0h, 1.0h - _BodyArt_Amount);
                color = lerp(_Color_BodyArt, color, bodyArtMask);
                return color.rgb;
            }

            inline void InitializeSurfaceDataCustom(float2 uv, out SurfaceData surfaceData)
            {
                surfaceData = (SurfaceData)0;
                half4 mask05 = SAMPLE_TEXTURE2D(_Mask_05, sampler_Mask_05, uv);
                half3 albedo = EvaluateCharacterAlbedo(uv);

                surfaceData.albedo = albedo;
                surfaceData.specular = half3(0.04h, 0.04h, 0.04h);
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = saturate(_Smoothness * 0.75h);
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
                surfaceData.emission = albedo * ((1.0h - mask05.r) * _Emission);
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = 1.0h;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;
            }

            inline void InitializeInputDataCustom(Varyings input, out InputData inputData)
            {
                inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0.0h, 0.0h, 0.0h);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            #if defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #else
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _Texture);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
                OUTPUT_SH(output.normalWS, output.vertexSH);
                output.positionCS = positionInputs.positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surfaceData;
                InitializeSurfaceDataCustom(input.uv, surfaceData);

                InputData inputData;
                InitializeInputDataCustom(input, inputData);

                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                color.rgb = lerp(color.rgb, surfaceData.albedo, 0.15h);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
