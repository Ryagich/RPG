Shader "RPG/Skybox/Low Poly Clouds"
{
    Properties
    {
        [Header(Sky)]
        _ZenithColor ("Zenith", Color) = (0.12, 0.36, 0.68, 1)
        _HorizonColor ("Horizon", Color) = (0.65, 0.81, 0.88, 1)
        _GroundColor ("Below Horizon", Color) = (0.24, 0.29, 0.32, 1)
        _SkyGradient ("Sky Gradient", Range(0.01, 16)) = 0.65
        _Exposure ("Exposure", Range(0, 32)) = 1

        [Header(Cloud Lighting and Projection)]
        _CloudLightColor ("Lit Facets", Color) = (1, 0.97, 0.88, 1)
        _CloudShadowColor ("Shaded Facets", Color) = (0.42, 0.54, 0.66, 1)
        _CloudProjectionBlend ("Projection Blend (Flat To Dome)", Range(0, 1)) = 1
        _CloudDomeCurvature ("Dome Curvature", Range(0.01, 8)) = 0.75
        _CloudVolume ("Apparent Thickness", Range(0, 12)) = 1.4
        _CloudLighting ("Sun Lighting", Range(0, 1)) = 0.7
        _CloudRimStrength ("Sunlit Edges", Range(0, 4)) = 0.2
        _FacetContrast ("Facet Contrast", Range(0, 4)) = 0.45
        _CloudHorizonFade ("Horizon Fade Height", Range(0.021, 1)) = 0.16

        [Header(Cloud Blur)]
        [Toggle(_CLOUD_BLUR)] _CloudBlur ("Enable Cloud Blur", Float) = 0
        _CloudBlurRadius ("Blur Radius", Range(0, 0.5)) = 0.015

        [Header(Baked Cloud Textures)]
        [NoScaleOffset] _CloudField ("Near Baked Field", 2D) = "black" {}
        [NoScaleOffset] _UpperCloudField ("Distant Baked Field", 2D) = "black" {}
        [HideInInspector] _CloudFieldParams ("Near Bake Metadata", Vector) = (32, 0, 0, 0)
        [HideInInspector] _UpperCloudFieldParams ("Distant Bake Metadata", Vector) = (32, 0, 0, 0)

        [Header(Near Cloud Layer)]
        _CloudCoverageMin ("Coverage Min", Range(0, 1)) = 0.47
        _CloudCoverageMax ("Coverage Max", Range(0, 1)) = 0.57
        _CloudScale ("Cloud Frequency", Range(0.01, 64)) = 2.2
        _CloudOpacity ("Opacity", Range(0, 1)) = 1
        _WindDirection ("Wind Direction (Degrees)", Range(0, 360)) = 25
        _WindSpeed ("Wind Speed", Range(-2, 2)) = 0.015
        _CloudWindStretch ("Stretch Along Wind", Range(0.01, 64)) = 1
        _CloudOffset ("Pattern Offset (XY)", Vector) = (13.7, 9.2, 0, 0)

        [Header(Distant Cloud Layer)]
        _UpperCloudCoverageMin ("Coverage Min", Range(0, 1)) = 0.63
        _UpperCloudCoverageMax ("Coverage Max", Range(0, 1)) = 0.73
        _UpperCloudScale ("Cloud Frequency", Range(0.01, 64)) = 4.5
        _UpperCloudOpacity ("Opacity (Zero Disables Layer)", Range(0, 1)) = 0.65
        _UpperCloudWindDirection ("Wind Direction (Degrees)", Range(0, 360)) = 315
        _UpperCloudWindSpeed ("Wind Speed", Range(-2, 2)) = 0.012
        _UpperCloudWindStretch ("Stretch Along Wind", Range(0.01, 64)) = 2
        _UpperCloudOffset ("Pattern Offset (XY)", Vector) = (-17.2, 35.6, 0, 0)
        _UpperCloudHaze ("Distance Haze", Range(0, 1)) = 0.3

        [Header(Sun)]
        [Toggle] _UseMainLight ("Follow Main Directional Light", Float) = 1
        _SunAzimuth ("Manual Azimuth (Degrees)", Range(0, 360)) = 135
        _SunElevation ("Manual Elevation (Degrees)", Range(-90, 90)) = 35
        [HDR] _SunColor ("Sun Color", Color) = (1, 0.82, 0.5, 1)
        _SunIntensity ("Sun Intensity", Range(0, 100)) = 4
        _SunDiskVisibility ("Sun Disk Visibility", Range(0, 1)) = 1
        _SunRadius ("Sun Radius (Degrees)", Range(0.01, 45)) = 1.5
        _SunHaloSize ("Halo Radius Multiplier", Range(1, 64)) = 6
        _SunHaloIntensity ("Halo Intensity", Range(0, 8)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Skybox"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ _CLOUD_BLUR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _HorizonColor;
                float4 _GroundColor;
                float4 _CloudLightColor;
                float4 _CloudShadowColor;
                float4 _CloudOffset;
                float4 _UpperCloudOffset;
                float4 _SunColor;
                float4 _CloudFieldParams;
                float4 _UpperCloudFieldParams;
                float _CloudCoverageMin;
                float _CloudCoverageMax;
                float _UpperCloudCoverageMin;
                float _UpperCloudCoverageMax;
                float _SkyGradient;
                float _Exposure;
                float _CloudScale;
                float _CloudProjectionBlend;
                float _CloudDomeCurvature;
                float _CloudVolume;
                float _CloudLighting;
                float _CloudRimStrength;
                float _FacetContrast;
                float _CloudOpacity;
                float _CloudHorizonFade;
                float _CloudBlurRadius;
                float _WindDirection;
                float _WindSpeed;
                float _CloudWindStretch;
                float _UpperCloudScale;
                float _UpperCloudOpacity;
                float _UpperCloudWindDirection;
                float _UpperCloudWindSpeed;
                float _UpperCloudWindStretch;
                float _UpperCloudHaze;
                float _UseMainLight;
                float _SunAzimuth;
                float _SunElevation;
                float _SunIntensity;
                float _SunDiskVisibility;
                float _SunRadius;
                float _SunHaloSize;
                float _SunHaloIntensity;
            CBUFFER_END

            TEXTURE2D(_CloudField);
            SAMPLER(sampler_CloudField);
            TEXTURE2D(_UpperCloudField);
            SAMPLER(sampler_UpperCloudField);

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;
                output.direction = input.positionOS.xyz;
                return output;
            }

            #include "Clouds/CloudFieldSampling.hlsl"

            float4 CloudLayer(float2 domeUV, float3 direction, float3 sunDirection,
                float2 coverage, float scale, float opacity, float windAngle, float speed,
                float stretch, float2 offset, bool distantLayer)
            {
                // Metadata Z marks a completed bake.
                float4 parameters = distantLayer ? _UpperCloudFieldParams : _CloudFieldParams;
                [branch] if (opacity <= 0.0 || parameters.z < 0.5)
                    return 0.0;

                float2 wind = float2(cos(radians(windAngle)), sin(radians(windAngle)));
                float2 acrossWind = float2(-wind.y, wind.x);
                float safeStretch = max(stretch, 0.01);
                float2 cloudUV = float2(dot(domeUV, wind), dot(domeUV, acrossWind)) * scale;
                // Apply movement before stretch to preserve feature speed.
                cloudUV.x = (cloudUV.x - _Time.y * speed) / safeStretch;
                cloudUV += offset;
                float2 limits = saturate(float2(min(coverage.x, coverage.y), max(coverage.x, coverage.y)));
                float threshold = limits.x;
                float2 fieldUV = cloudUV / max(parameters.x, 0.01);
                // Let the repeat sampler wrap: frac here would break mip derivatives.
                float4 field = distantLayer
                    ? SAMPLE_TEXTURE2D(_UpperCloudField, sampler_UpperCloudField, fieldUV)
                    : SAMPLE_TEXTURE2D(_CloudField, sampler_CloudField, fieldUV);
                field = CloudFieldToSurface(field, threshold, parameters.y);
                float mask = CloudCoverageMask(field.x, limits);
                mask *= smoothstep(0.02, max(_CloudHorizonFade, 0.021), direction.y) * opacity;

                float2 slope = wind * (field.z / safeStretch) + acrossWind * field.w;
                float normalCurvature = _CloudDomeCurvature * saturate(_CloudProjectionBlend);
                float3 domeNormal = normalize(float3(direction.x * normalCurvature,
                    1.0, direction.z * normalCurvature));
                float3 tangent = normalize(float3(domeNormal.y, -domeNormal.x, 0.0));
                float3 bitangent = cross(tangent, domeNormal);
                float3 normal = normalize(domeNormal - _CloudVolume
                    * (slope.x * tangent + slope.y * bitangent));
                float diffuse = saturate(dot(normal, sunDirection));
                float daylight = smoothstep(-0.06, 0.08, sunDirection.y);
                float faceTint = saturate(0.55 + (field.y - 0.5) * (1.0 + _FacetContrast * 5.0));
                float lighting = lerp(faceTint, diffuse * 0.85 + faceTint * 0.15, _CloudLighting * daylight);
                float3 color = lerp(_CloudShadowColor.rgb, _CloudLightColor.rgb, lighting);

                float facingSun = saturate(dot(direction, sunDirection));
                facingSun *= facingSun;
                facingSun *= facingSun;
                facingSun *= facingSun;
                float rim = saturate(1.0 - (field.x - threshold) * 8.0) * facingSun
                    * _CloudRimStrength * daylight * saturate(_SunIntensity);
                color += _CloudLightColor.rgb * rim;
                return float4(color, mask);
            }

            #include "Clouds/CloudLayerBlur.hlsl"

            float3 SunDirection()
            {
                float azimuth = radians(_SunAzimuth);
                float elevation = radians(_SunElevation);
                float3 manualDirection = float3(sin(azimuth) * cos(elevation),
                    sin(elevation), cos(azimuth) * cos(elevation));
                float mainLengthSq = dot(_MainLightPosition.xyz, _MainLightPosition.xyz);
                float follow = step(0.5, _UseMainLight) * (1.0 - step(0.5, _MainLightPosition.w))
                    * step(0.0001, mainLengthSq);
                return lerp(manualDirection,
                    _MainLightPosition.xyz * rsqrt(max(mainLengthSq, 0.0001)), follow);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.direction);
                float skyHeight = pow(saturate(direction.y), _SkyGradient);
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, skyHeight);
                sky = lerp(sky, _GroundColor.rgb, smoothstep(0.0, 0.25, -direction.y));

                float3 skyBackground = sky;
                float3 sunDirection = SunDirection();
                float sunDistance = length(direction - sunDirection);
                float sunRadius = 2.0 * sin(radians(_SunRadius) * 0.5);
                float sunAA = max(fwidth(sunDistance), 0.00001);
                float sunDisk = 1.0 - smoothstep(sunRadius - sunAA, sunRadius + sunAA, sunDistance);
                float haloRadius = max(sunRadius * _SunHaloSize, 0.0001);
                float halo = exp2(-4.0 * sunDistance * sunDistance / (haloRadius * haloRadius));
                float aboveHorizon = smoothstep(-0.01, 0.02, direction.y);
                sky += _SunColor.rgb * _SunIntensity
                    * (sunDisk + halo * _SunHaloIntensity) * aboveHorizon * saturate(_SunDiskVisibility);

                float curvature = max(_CloudDomeCurvature, 0.01);
                float2 domeUV = direction.xz * (1.0 + curvature) / (max(direction.y, 0.0) + curvature);
                float2 flatUV = direction.xz / max(direction.y, 0.04);
                float2 cloudUV = lerp(flatUV, domeUV, saturate(_CloudProjectionBlend));
                float4 distant = RenderCloudLayer(cloudUV, direction, sunDirection,
                    float2(_UpperCloudCoverageMin, _UpperCloudCoverageMax), _UpperCloudScale, _UpperCloudOpacity,
                    _UpperCloudWindDirection, _UpperCloudWindSpeed, _UpperCloudWindStretch,
                    _UpperCloudOffset.xy, true);
                distant.rgb = lerp(distant.rgb, skyBackground, _UpperCloudHaze);
                sky = lerp(sky, distant.rgb, distant.a);
                float4 nearCloud = RenderCloudLayer(cloudUV, direction, sunDirection,
                    float2(_CloudCoverageMin, _CloudCoverageMax), _CloudScale, _CloudOpacity,
                    _WindDirection, _WindSpeed, _CloudWindStretch,
                    _CloudOffset.xy, false);
                return half4(lerp(sky, nearCloud.rgb, nearCloud.a) * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
    CustomEditor "EditorTools.CloudSkyboxShaderGUI"
}
