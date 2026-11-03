#ifndef RPG_CLOUD_SMOOTH_NOISE_INCLUDED
#define RPG_CLOUD_SMOOTH_NOISE_INCLUDED

#include "CloudNoiseCommon.hlsl"

// Returns value, d/dx, d/dy.
float3 CloudSmoothNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * f * f * (f - 1.0) * (f - 1.0);
    float a = Hash21(cell);
    float b = Hash21(cell + float2(1, 0));
    float c = Hash21(cell + float2(0, 1));
    float d = Hash21(cell + 1.0);
    float crossTerm = a - b - c + d;
    float value = a + (b - a) * u.x + (c - a) * u.y + crossTerm * u.x * u.y;
    float2 gradient = float2(b - a + crossTerm * u.y, c - a + crossTerm * u.x) * du;
    return float3(value, gradient);
}

float3 CloudScaledNoise(float2 p, float2 frequency, float2 offset)
{
    float3 noise = CloudSmoothNoise(p * frequency + offset);
    noise.yz *= frequency;
    return noise;
}

float3 CloudNoiseRamp(float3 noise, float lower, float upper)
{
    float t = saturate((noise.x - lower) / (upper - lower));
    return float3(t * t * (3.0 - 2.0 * t),
        noise.yz * (6.0 * t * (1.0 - t) / (upper - lower)));
}

#endif
