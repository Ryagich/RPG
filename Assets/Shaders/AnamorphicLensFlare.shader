Shader "Hidden/RPG/AnamorphicLensFlare"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        TEXTURE2D_X(_SeedTexture);
        TEXTURE2D_X(_StreakTexture);
        float4 _Filter, _Direction, _Strength, _Ghosts, _Tint, _Falloff;

        float Inside(float2 uv)
        {
            return step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
        }

        float ScreenFalloff(float2 uv)
        {
            float2 centered = abs(uv * 2.0 - 1.0);
            float distance = lerp(max(centered.x, centered.y), length(centered), _Falloff.y);
            float fade = 1.0 - smoothstep(1.0 - max(_Falloff.x, 0.0001), 1.0, distance);
            return (_Falloff.x > 0.0 ? fade : 1.0) * Inside(uv);
        }

        float3 Bright(float2 uv)
        {
            float3 color = min(max(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb, 0.0), _Filter.y);
            float brightness = max(color.r, max(color.g, color.b));
            float knee = max(_Filter.x * 0.5, 0.0001);
            float soft = clamp(brightness - _Filter.x + knee, 0.0, 2.0 * knee);
            soft = soft * soft / (4.0 * knee);
            return color * max(soft, brightness - _Filter.x) / max(brightness, 0.0001) * ScreenFalloff(uv);
        }

        half4 Prefilter(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 offset = _BlitTexture_TexelSize.xy;
            return half4((Bright(uv + offset) + Bright(uv - offset)
                + Bright(uv + float2(offset.x, -offset.y))
                + Bright(uv + float2(-offset.x, offset.y))) * 0.25, 1.0);
        }

        half4 Blur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 color = 0.0;
            [unroll] for (int i = -4; i <= 4; i++)
            {
                float2 uv = input.texcoord + _Direction.xy * i;
                float weight = (5.0 - abs(i)) / 25.0;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * weight * Inside(uv);
            }
            return half4(color * _Direction.z, 0.0);
        }

        float3 Seed(float2 uv)
        {
            // Source falloff was applied before blur/flare generation; samples outside are black.
            return SAMPLE_TEXTURE2D_X(_SeedTexture, sampler_LinearClamp, uv).rgb * Inside(uv);
        }

        half4 Composite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            float3 streak = SAMPLE_TEXTURE2D_X(_StreakTexture, sampler_LinearClamp, uv).rgb;
            float2 axis = 0.5 - uv;
            float2 separation = normalize(axis + float2(0.00001, 0.0))
                * float2(1.0 / _Strength.z, 1.0) * _Strength.w;
            float3 ghosts = 0.0;
            [loop] for (int i = 0; i < (int)_Ghosts.x; i++)
            {
                float2 sampleUV = 1.0 - uv + axis * (_Ghosts.y * i);
                float3 ghost = float3(Seed(sampleUV + separation).r, Seed(sampleUV).g, Seed(sampleUV - separation).b);
                ghosts += ghost / (1.0 + i);
            }
            float falloff = ScreenFalloff(uv);
            return half4(original.rgb + _Tint.rgb * (streak * _Strength.x + ghosts * _Strength.y) * falloff, original.a);
        }
        ENDHLSL
        Pass
        {
            Name "Bright Sources"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Prefilter
            ENDHLSL
        }
        Pass
        {
            Name "Anamorphic Streak"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Blur
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
        Pass
        {
            Name "Accumulate Direction"
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Blur
            ENDHLSL
        }
    }
    Fallback Off
}
