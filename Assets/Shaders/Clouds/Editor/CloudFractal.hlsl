// No include guard: each shape instantiates this function with its own macros.
#include "CloudFractalSettings.hlsl"

float4 RPG_CLOUD_FRACTAL_FUNCTION(float2 p, float detailAmount, CloudFractalSettings settings)
{
    float4 sum = 0.0;
    float weightSum = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float2 octaveUV = p;

    [loop] for (int octave = 0; octave < settings.iterations; octave++)
    {
        float4 field = RPG_CLOUD_FRACTAL_BASIS(octaveUV, detailAmount);
        // Convert derivatives to base coordinates.
        field.zw *= frequency;
        sum += field * amplitude;
        weightSum += amplitude;
        amplitude *= settings.persistence;
        [branch] if (amplitude <= 0.0)
            break;
        frequency *= settings.lacunarity;
        octaveUV = octaveUV * settings.lacunarity + float2(7.1, 3.8);
    }

    return sum / weightSum;
}

#undef RPG_CLOUD_FRACTAL_FUNCTION
#undef RPG_CLOUD_FRACTAL_BASIS
