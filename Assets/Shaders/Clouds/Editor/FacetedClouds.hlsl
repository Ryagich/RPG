#ifndef RPG_FACETED_CLOUDS_INCLUDED
#define RPG_FACETED_CLOUDS_INCLUDED

#include "CloudNoiseCommon.hlsl"

float4 TriangleNoise(float2 p)
{
    float2 lattice = float2(p.x - p.y * 0.577350269, p.y * 1.154700538);
    float2 cell = floor(lattice);
    float2 f = frac(lattice);
    float upper = step(1.0, f.x + f.y);
    float a = Hash21(cell + upper);
    float b = Hash21(cell + float2(1, 0));
    float c = Hash21(cell + float2(0, 1));
    float3 weights = lerp(float3(1.0 - f.x - f.y, f.x, f.y),
        float3(f.x + f.y - 1.0, 1.0 - f.y, 1.0 - f.x), upper);
    float density = dot(float3(a, b, c), weights);
    float2 latticeGradient = lerp(float2(b - a, c - a), float2(a - c, a - b), upper);
    float2 gradient = float2(latticeGradient.x,
        latticeGradient.y * 1.154700538 - latticeGradient.x * 0.577350269);
    return float4(density, (a + b + c) / 3.0, gradient);
}

float4 FacetedCloudField(float2 p, float detailAmount)
{
    float4 field = TriangleNoise(p);
    [branch] if (detailAmount > 0.0)
    {
        float4 detail = TriangleNoise(p * 2.03 + float2(7.1, 3.8));
        detail.zw *= 2.03;
        field = lerp(field, detail, detailAmount);
    }
    return field;
}

#define RPG_CLOUD_FRACTAL_FUNCTION FractalFacetedCloudField
#define RPG_CLOUD_FRACTAL_BASIS FacetedCloudField
#include "CloudFractal.hlsl"

#endif
