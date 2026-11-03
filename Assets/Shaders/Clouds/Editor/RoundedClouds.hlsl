#ifndef RPG_ROUNDED_CLOUDS_INCLUDED
#define RPG_ROUNDED_CLOUDS_INCLUDED

#include "CloudNoiseCommon.hlsl"

// Support including jitter must stay below cell spacing (4) for the 3x3 search.
float4 RoundedCloudField(float2 p, float detailAmount)
{
    float2 cell = floor(p / 4.0);
    float densitySum = 0.0;
    float2 gradientSum = 0.0;
    [loop] for (int y = -1; y <= 1; y++)
    {
        [loop] for (int x = -1; x <= 1; x++)
        {
            float2 id = cell + float2(x, y);
            float2 centre = (id + 0.5) * 4.0;
            centre += (float2(Hash21(id + 11.3), Hash21(id + 37.9)) - 0.5) * 2.4;
            [unroll] for (int puff = 0; puff < 5; puff++)
            {
                float2 seed = id + float2(puff * 17.13, puff * 7.71);
                float randomRadius = Hash21(seed + 5.7);
                float radius = 0.78 + (randomRadius - 0.5) * (0.3 + detailAmount);
                float2 puffCentre = centre + float2((puff - 2.0) * 0.65,
                    (Hash21(seed + 23.4) - 0.5) * 0.5);
                float2 inverseRadius = rcp(float2(radius * 1.15, radius));
                float2 delta = (p - puffCentre) * inverseRadius;
                float cap = saturate(1.0 - dot(delta, delta));
                densitySum += cap * cap;
                gradientSum += -4.0 * cap * delta * inverseRadius;
            }
        }
    }
    float transmission = exp2(-4.0 * densitySum);
    float density = 1.0 - transmission;
    float2 gradient = (4.0 * 0.693147181) * transmission * gradientSum;
    return float4(density, 0.5 + 0.15 * density, gradient);
}

#define RPG_CLOUD_FRACTAL_FUNCTION FractalRoundedCloudField
#define RPG_CLOUD_FRACTAL_BASIS RoundedCloudField
#include "CloudFractal.hlsl"

#endif
