#ifndef RPG_CLOUD_LAYER_BLUR_INCLUDED
#define RPG_CLOUD_LAYER_BLUR_INCLUDED

// Premultiplied filtering prevents colour fringes at transparent edges.
float4 RenderCloudLayer(float2 domeUV, float3 direction, float3 sunDirection,
    float2 coverage, float scale, float opacity, float windAngle, float speed,
    float stretch, float2 offset, bool distantLayer)
{
    float4 centre = CloudLayer(domeUV, direction, sunDirection,
        coverage, scale, opacity, windAngle, speed,
        stretch, offset, distantLayer);

    #if defined(_CLOUD_BLUR)
        [branch] if (_CloudBlurRadius > 0.0 && opacity > 0.0)
        {
            float4 filtered = float4(centre.rgb * centre.a, centre.a) * 0.5;
            const float2 taps[4] = {
                float2(-1.0, 0.0), float2(1.0, 0.0),
                float2(0.0, -1.0), float2(0.0, 1.0)
            };
            [unroll] for (int tap = 0; tap < 4; tap++)
            {
                float4 sampleCloud = CloudLayer(domeUV + taps[tap] * _CloudBlurRadius,
                    direction, sunDirection, coverage, scale, opacity, windAngle,
                    speed, stretch, offset, distantLayer);
                filtered += float4(sampleCloud.rgb * sampleCloud.a, sampleCloud.a) * 0.125;
            }
            return float4(filtered.rgb / max(filtered.a, 0.000001), filtered.a);
        }
    #endif

    return centre;
}

#endif
