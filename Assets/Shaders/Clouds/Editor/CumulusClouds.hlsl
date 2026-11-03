#ifndef RPG_CUMULUS_CLOUDS_INCLUDED
#define RPG_CUMULUS_CLOUDS_INCLUDED

#include "CloudSmoothNoise.hlsl"

// Support including jitter must stay below cell spacing for the 3x3 search.
float4 CumulusCloudField(float2 p, float detailAmount)
{
    float detail = saturate(detailAmount * 2.0);
    float2 cell = floor(p / 6.0);
    float densitySum = 0.0;
    float2 gradientSum = 0.0;
    [loop] for (int y = -1; y <= 1; y++)
    {
        [loop] for (int x = -1; x <= 1; x++)
        {
            float2 id = cell + float2(x, y);
            float2 centre = (id + 0.5) * 6.0;
            centre += (float2(Hash21(id + 11.3), Hash21(id + 37.9)) - 0.5) * 1.8;
            [unroll] for (int puff = 0; puff < 7; puff++)
            {
                float2 seed = id + float2(puff * 17.13, puff * 7.71);
                float angle = Hash21(seed + 43.2) * 6.283185307;
                float distance = puff == 0 ? 0.0 : sqrt(Hash21(seed + 71.4)) * 1.6;
                float radius = puff == 0 ? 1.5 : 0.65 + Hash21(seed + 5.7) * 0.65;
                float2 puffCentre = centre + float2(cos(angle), sin(angle)) * distance;
                float2 inverseRadius = rcp(float2(radius * 1.1, radius));
                float2 delta = (p - puffCentre) * inverseRadius;
                float cap = saturate(1.0 - dot(delta, delta));
                densitySum += cap * cap;
                gradientSum += -4.0 * cap * delta * inverseRadius;
            }
        }
    }
    float transmission = exp2(-2.2 * densitySum);
    float body = 1.0 - transmission;
    float2 bodyGradient = (2.2 * 0.693147181) * transmission * gradientSum;

    float3 coarse = CloudScaledNoise(p, float2(1.8, 1.8), float2(8.1, 17.6));
    float3 fine = CloudScaledNoise(p, float2(4.6, 4.6), float2(31.7, 5.2));
    float3 erosion = lerp(coarse, fine, detail * 0.35);
    float amount = lerp(0.25, 0.8, detail);
    float edge = body * (1.0 - body);
    float density = body - amount * (1.0 - erosion.x) * edge;
    float2 gradient = bodyGradient * (1.0 - amount * (1.0 - erosion.x) * (1.0 - 2.0 * body))
        + amount * edge * erosion.yz;
    float tint = 0.5 + 0.12 * body + 0.06 * (coarse.x - 0.5);
    return float4(density, tint, gradient);
}

#define RPG_CLOUD_FRACTAL_FUNCTION FractalCumulusCloudField
#define RPG_CLOUD_FRACTAL_BASIS CumulusCloudField
#include "CloudFractal.hlsl"

#endif
