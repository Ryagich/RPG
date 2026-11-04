#ifndef RPG_DEPTH_WATER_INCLUDED
#define RPG_DEPTH_WATER_INCLUDED

TEXTURE2D(_NormalA); SAMPLER(sampler_NormalA);
TEXTURE2D(_NormalB); SAMPLER(sampler_NormalB);
TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
TEXTURE2D(_SurfaceTex); SAMPLER(sampler_SurfaceTex);
TEXTURE2D(_CausticsTex); SAMPLER(sampler_CausticsTex);

CBUFFER_START(UnityPerMaterial)
    float4 _ShallowColor, _DeepColor, _SurfaceColor;
    float4 _NormalTiling, _NormalSpeed, _NoiseSpeed, _SurfaceSpeed, _CausticsSpeed;
    float _ColorDepth, _Absorption, _ShoreBlend, _NormalScale;
    float _NoiseTiling, _Refraction, _BottomDispersion;
    float _SurfaceTiling, _SurfaceDistortion;
    float _CausticsTiling, _CausticsStrength, _CausticsDepth, _CausticsDispersion, _CausticsDistortion;
    float _Smoothness, _SpecularStrength, _FresnelStrength;
    float _GlintRoughness, _SunPathStretch, _GlintDetailTiling, _GlintDetailStrength;
CBUFFER_END

struct WaterAttributes
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct WaterVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half fog : TEXCOORD1;
    half3 vertexLighting : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

WaterVaryings WaterVertex(WaterAttributes input)
{
    WaterVaryings output = (WaterVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.fog = ComputeFogFactor(position.positionCS.z);
    output.vertexLighting = VertexLighting(position.positionWS, half3(0, 1, 0));
    return output;
}

// A horizontal water plane owns the surface height. Reconstructing from inverse
// VP also works for orthographic cameras and reversed Z, unlike eye-depth deltas.
float3 WaterBottomPosition(float2 uv, out float valid)
{
    float raw = SampleSceneDepth(uv);
    #if UNITY_REVERSED_Z
        valid = step(0.000001, raw);
        float depth = raw;
    #else
        valid = 1.0 - step(0.999999, raw);
        float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
    #endif
    // Avoid infinite world coordinates when a far plane is at infinity.
    #if UNITY_REVERSED_Z
        depth = max(depth, 0.000001);
    #else
        depth = min(depth, 0.999999);
    #endif
    return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
}

float2 WaterSafeRefractionUV(float2 uv, float2 offset, float surfaceY)
{
    float2 candidate = saturate(uv + offset);
    float valid;
    float3 bottom = WaterBottomPosition(candidate, valid);
    // Reject geometry above the water independently for each colour channel.
    // The original UV is safe because the water already passed the depth test.
    return lerp(uv, candidate, valid * step(bottom.y + 0.001, surfaceY));
}

half WaterCausticsSample(float2 uv)
{
    half a = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv).r;
    half b = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex,
        uv * 1.23 + float2(0.37, 0.61) - _Time.y * _CausticsSpeed.xy * 1.7).r;
    return min(a, b);
}

half WaterBottomShadow(float3 bottomWS)
{
    // Screen-space shadows describe the visible surface, not the reconstructed
    // receiver. Sample the directional shadow atlas at the bottom in every mode.
    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        float4 coord = TransformWorldToShadowCoord(bottomWS);
        half shadow = MainLightRealtimeShadow(coord);
        return lerp(shadow, 1.0h, GetMainLightShadowFade(bottomWS));
    #else
        return 1.0h;
    #endif
}

struct WaterLighting
{
    half3 volume;
    half3 surface;
    float3 specular;
};

bool WaterLightAffectsMesh(Light light)
{
    #if defined(_LIGHT_LAYERS)
        return IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer());
    #else
        return true;
    #endif
}

// An anisotropic GGX lobe: the long axis follows the light's horizontal
// direction, so stretching it covers more wave slopes along the sun path.
// This returns BRDF * NdotL, including Smith visibility and water Fresnel.
float WaterGlint(float3 lightWS, float3 normalWS, float3 viewWS, float alpha, float stretch)
{
    float NdotL = saturate(dot(normalWS, lightWS));
    float NdotV = saturate(dot(normalWS, viewWS));
    if (NdotL <= 0.0 || NdotV <= 0.0 || lightWS.y <= 0.0)
        return 0.0;
    float3 halfWS = SafeNormalize(viewWS + lightWS);
    float3 axis = float3(lightWS.x, 0, lightWS.z);
    axis = dot(axis, axis) > 1e-6 ? normalize(axis) : float3(1, 0, 0);
    float3 tangent = axis - normalWS * dot(axis, normalWS);
    if (dot(tangent, tangent) < 1e-6)
        tangent = cross(normalWS, float3(0, 0, 1));
    tangent = SafeNormalize(tangent);
    float3 bitangent = cross(normalWS, tangent);
    float alphaLong = min(alpha * stretch, 1.0);
    float alphaShort = alpha;
    float TdotH = dot(tangent, halfWS);
    float BdotH = dot(bitangent, halfWS);
    float NdotH = saturate(dot(normalWS, halfWS));
    float3 scaledHalf = float3(TdotH / alphaLong, BdotH / alphaShort, NdotH);
    float denominator = dot(scaledHalf, scaledHalf);
    float distribution = rcp(PI * alphaLong * alphaShort * denominator * denominator);
    float projectedV = length(float3(alphaLong * dot(tangent, viewWS), alphaShort * dot(bitangent, viewWS), NdotV));
    float projectedL = length(float3(alphaLong * dot(tangent, lightWS), alphaShort * dot(bitangent, lightWS), NdotL));
    float visibility = 0.5 / max(NdotL * projectedV + NdotV * projectedL, 1e-5);
    float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(viewWS, halfWS)), 5.0);
    return distribution * visibility * fresnel * NdotL;
}

void WaterAccumulateLight(Light light, half3 normalWS, half3 glintNormalWS, half3 viewWS,
    float glintAlpha, float stretch, inout WaterLighting lighting)
{
    if (!WaterLightAffectsMesh(light))
        return;
    float3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);
    // The depth tint represents scattering in the water column. Use the plane
    // normal for it; the normal maps describe the water/air interface and foam.
    lighting.volume += radiance * saturate(light.direction.y);
    half NdotL = saturate(dot(normalWS, light.direction));
    lighting.surface += radiance * NdotL;
    float highlight = WaterGlint(light.direction, glintNormalWS, viewWS, glintAlpha, stretch);
    lighting.specular += radiance * (highlight * _SpecularStrength);
}

WaterLighting WaterEvaluateLighting(WaterVaryings input, float2 screenUV, half3 normalWS,
    half3 glintNormalWS, half3 viewWS, float glintAlpha, Light sun)
{
    WaterLighting lighting = (WaterLighting)0;
    lighting.volume = SampleSH(half3(0, 1, 0));
    lighting.surface = SampleSH(normalWS);
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
        lighting.volume += input.vertexLighting;
        lighting.surface += input.vertexLighting;
    #endif
    sun.shadowAttenuation = WaterBottomShadow(input.positionWS);
    float lowSun = 1.0 - saturate(sun.direction.y);
    float sunStretch = lerp(1.0, max(_SunPathStretch, 1.0), lowSun * lowSun);
    WaterAccumulateLight(sun, normalWS, glintNormalWS, viewWS, glintAlpha, sunStretch, lighting);

    #if defined(_ADDITIONAL_LIGHTS)
        // URP's clustered loop reads these two fields from inputData.
        InputData inputData = (InputData)0;
        inputData.positionWS = input.positionWS;
        inputData.normalizedScreenSpaceUV = screenUV;
        #if USE_CLUSTER_LIGHT_LOOP
            [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
            {
                CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                WaterAccumulateLight(light, normalWS, glintNormalWS, viewWS, glintAlpha, 1.0, lighting);
            }
        #endif
        uint lightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(lightCount)
            Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
            WaterAccumulateLight(light, normalWS, glintNormalWS, viewWS, glintAlpha, 1.0, lighting);
        LIGHT_LOOP_END
    #endif
    return lighting;
}

half4 WaterFragment(WaterVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
    float validBottom;
    float3 bottomWS = WaterBottomPosition(uv, validBottom);
    float columnDepth = max(input.positionWS.y - bottomWS.y, 0.0);
    float rayDepth = distance(input.positionWS, bottomWS);
    // Sky has no receiver, but should still render as deep water.
    columnDepth = lerp(max(_ColorDepth, _CausticsDepth) * 8.0, columnDepth, validBottom);
    rayDepth = lerp(columnDepth, rayDepth, validBottom);
    float shore = smoothstep(0.0, max(_ShoreBlend, 0.0001), columnDepth);
    float2 worldUV = input.positionWS.xz;
    float2 noiseUV = worldUV * _NoiseTiling + _Time.y * _NoiseSpeed.xy;
    float2 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rg * 2.0 - 1.0;
    half3 normalA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalA, sampler_NormalA,
        worldUV * _NormalTiling.xy + _Time.y * _NormalSpeed.xy), _NormalScale);
    half3 normalB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalB, sampler_NormalB,
        worldUV * _NormalTiling.zw + _Time.y * _NormalSpeed.zw), _NormalScale);
    half3 tangentNormal = BlendNormalRNM(SafeNormalize(normalA), SafeNormalize(normalB));
    tangentNormal = SafeNormalize(half3(tangentNormal.xy, max(tangentNormal.z, 0.001h)));
    half3 normalWS = half3(tangentNormal.x, tangentNormal.z, tangentNormal.y);
    // Reuse the assigned second normal map for moving capillary-scale ripples.
    // They affect direct glints only, preserving the larger reflection/refraction waves.
    half3 detailNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalB, sampler_NormalB,
        worldUV * max(_GlintDetailTiling, 0.0) + _Time.y * _NormalSpeed.zw),
        _GlintDetailStrength * _NormalScale);
    half3 glintTangent = BlendNormalRNM(tangentNormal, SafeNormalize(detailNormal));
    glintTangent = SafeNormalize(half3(glintTangent.xy, max(glintTangent.z, 0.001h)));
    half3 glintNormalWS = half3(glintTangent.x, glintTangent.z, glintTangent.y);
    // Filter unresolved normal detail into roughness rather than unstable fireflies.
    float3 normalDx = ddx(glintNormalWS);
    float3 normalDy = ddy(glintNormalWS);
    float normalVariance = 0.25 * (dot(normalDx, normalDx) + dot(normalDy, normalDy));
    float roughness = clamp(max(1.0 - _Smoothness, _GlintRoughness), 0.045, 1.0);
    float glintAlpha = min(sqrt(Pow4(roughness) + normalVariance), 1.0);
    float2 distortion = (noise + tangentNormal.xy) * shore;
    float2 offset = distortion * _Refraction * saturate(rayDepth);
    // Correct screen aspect so a circular noise feature refracts equally on both axes.
    offset.x *= _ScaledScreenParams.y / _ScaledScreenParams.x;
    float2 uvR = WaterSafeRefractionUV(uv, offset * (1.0 + _BottomDispersion), input.positionWS.y);
    float2 uvG = WaterSafeRefractionUV(uv, offset, input.positionWS.y);
    float2 uvB = WaterSafeRefractionUV(uv, offset * (1.0 - _BottomDispersion), input.positionWS.y);
    half3 bottomColor = half3(SampleSceneColor(uvR).r, SampleSceneColor(uvG).g, SampleSceneColor(uvB).b);

    // Anchor caustics to the same refracted receiver as the green colour channel.
    float receiverValid;
    float3 receiverWS = WaterBottomPosition(uvG, receiverValid);
    float3 bottomCross = cross(ddy(receiverWS), ddx(receiverWS));
    float3 bottomNormal = bottomCross * rsqrt(max(dot(bottomCross, bottomCross), 1e-12));
    bottomNormal *= bottomNormal.y < 0.0 ? -1.0 : 1.0;
    float receiverDepth = max(input.positionWS.y - receiverWS.y, 0.0);
    Light sun = GetMainLight();
    if (!WaterLightAffectsMesh(sun))
        sun.color = 0;
    float3 lightRay = refract(-sun.direction, float3(0, 1, 0), 1.0 / 1.333);
    float3 entryWS = receiverWS - lightRay * (receiverDepth / max(-lightRay.y, 0.05));
    float2 causticsUV = entryWS.xz * _CausticsTiling + _Time.y * _CausticsSpeed.xy + noise * _CausticsDistortion;
    float2 chromaticOffset = float2(_CausticsDispersion, _CausticsDispersion * 0.5);
    half3 caustics = half3(WaterCausticsSample(causticsUV + chromaticOffset),
        WaterCausticsSample(causticsUV), WaterCausticsSample(causticsUV - chromaticOffset));
    half sunAboveWater = saturate(sun.direction.y);
    half receiverFacing = saturate(dot(bottomNormal, -lightRay));
    // _CausticsDepth is the full fade-out depth in metres, not an exponential
    // e-folding distance (which left 37% of the pattern at the configured depth).
    float causticsDepthFade = 1.0 - smoothstep(0.0, max(_CausticsDepth, 0.001), receiverDepth);
    half causticsVisibility = receiverValid * validBottom * sunAboveWater * receiverFacing
        * sun.distanceAttenuation * WaterBottomShadow(receiverWS)
        * causticsDepthFade * shore;
    bottomColor += caustics * sun.color * (_CausticsStrength * causticsVisibility);

    half depthMix = 1.0 - exp(-columnDepth / max(_ColorDepth, 0.001));
    half3 waterTint = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthMix);
    half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    WaterLighting lighting = WaterEvaluateLighting(input, uv, normalWS, glintNormalWS, view, glintAlpha, sun);
    half transmission = exp(-max(_Absorption, 0.0) * rayDepth);
    // The opaque scene and caustics are already lit; only tint and foam are albedo.
    float3 color = lerp(waterTint * lighting.volume, bottomColor, transmission);
    half4 surface = SAMPLE_TEXTURE2D(_SurfaceTex, sampler_SurfaceTex,
        worldUV * _SurfaceTiling + _Time.y * _SurfaceSpeed.xy + distortion * _SurfaceDistortion);
    // Use the macroscopic interface for the environment Fresnel approximation.
    // Wave normals still steer reflection and highlights, but cannot turn isolated
    // texels into grazing-angle mirrors that replace the water with a dark probe.
    half fresnel = 0.02h + 0.98h * pow(1.0h - saturate(view.y), 5.0h);
    half3 reflection = GlossyEnvironmentReflection(reflect(-view, normalWS),
        input.positionWS, 1.0h - _Smoothness, 1.0h, uv);
    color = lerp(color, reflection, fresnel * _FresnelStrength);
    color += lighting.specular;
    // Foam covers the reflective water interface; its albedo must not be replaced
    // by the water's environment reflection, and must not glow in an unlit scene.
    half3 surfaceColor = surface.rgb * _SurfaceColor.rgb * lighting.surface;
    color = lerp(color, surfaceColor, saturate(surface.a * _SurfaceColor.a));
    // GGX peaks can exceed half precision at grazing angles. Keep the lighting
    // sum in float and bound only to the HDR output's representable range.
    color = MixFog(min(color, HALF_MAX), input.fog);
    return half4(color, shore);
}
#endif
