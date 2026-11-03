#ifndef RPG_CIRRUS_CLOUDS_INCLUDED
#define RPG_CIRRUS_CLOUDS_INCLUDED

#include "CloudSmoothNoise.hlsl"

float4 CirrusCloudField(float2 p, float detailAmount)
{
    float detail = saturate(detailAmount * 2.0);
    float3 bend = CloudScaledNoise(p, float2(0.13, 0.2), float2(13.4, 27.8));
    float2 q = float2(p.x * 0.22, p.y * 1.25 + (bend.x - 0.5) * 1.4);
    float2 qdx = float2(0.22, bend.y * 1.4);
    float2 qdy = float2(0.0, 1.25 + bend.z * 1.4);

    float3 fibres = CloudScaledNoise(q, float2(0.6, lerp(2.5, 5.5, detail)), float2(7.6, 19.3));
    // Apply the warp's Jacobian to the gradient.
    fibres.yz = float2(dot(fibres.yz, qdx), dot(fibres.yz, qdy));
    float signedRidge = fibres.x * 2.0 - 1.0;
    float ridge = 1.0 - signedRidge * signedRidge;
    float strand = ridge * ridge * ridge;
    float2 strandGradient = -12.0 * ridge * ridge * signedRidge * fibres.yz;

    float3 envelope = CloudNoiseRamp(
        CloudScaledNoise(p, float2(0.12, 0.32), float2(41.7, 3.2)), 0.3, 0.75);
    float3 breaks = CloudScaledNoise(p, float2(0.35, 0.8), float2(31.5, 52.1));
    float taper = 0.25 + 0.75 * breaks.x;
    float density = envelope.x * strand * taper;
    float2 gradient = envelope.yz * strand * taper
        + envelope.x * strandGradient * taper
        + envelope.x * strand * 0.75 * breaks.yz;
    return float4(density, 0.52 + 0.08 * density, gradient);
}

#define RPG_CLOUD_FRACTAL_FUNCTION FractalCirrusCloudField
#define RPG_CLOUD_FRACTAL_BASIS CirrusCloudField
#include "CloudFractal.hlsl"

#endif
