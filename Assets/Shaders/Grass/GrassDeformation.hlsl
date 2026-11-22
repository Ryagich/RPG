#ifndef RPG_GRASS_DEFORMATION_INCLUDED
#define RPG_GRASS_DEFORMATION_INCLUDED
Texture2D<float4> _RpgGrassField;
SamplerState sampler_RpgGrassField;
float4 _RpgGrassFieldRect; // xy = world minimum XZ, z = inverse size, w = enabled
float4 _RpgGrassBend; // horizontal, downward, height tolerance, max blade height

float3 RpgGrassOffset(float3 positionWS, float tipMask)
{
    if (_RpgGrassFieldRect.w < 0.5 || tipMask <= 0) return 0;
    float2 uv = (positionWS.xz - _RpgGrassFieldRect.xy) * _RpgGrassFieldRect.z;
    if (any(uv <= 0) || any(uv >= 1)) return 0;
    float4 field = _RpgGrassField.SampleLevel(sampler_RpgGrassField, uv, 0);
    if (field.b < 0.001) return 0;
    float relativeHeight = positionWS.y - field.a / field.b;
    float heightMask = smoothstep(-_RpgGrassBend.z, 0, relativeHeight)
        * (1 - smoothstep(_RpgGrassBend.w, _RpgGrassBend.w + _RpgGrassBend.z, relativeHeight));
    float mask = saturate(tipMask) * heightMask;
    float down = min(field.b * _RpgGrassBend.y * mask, max(0, relativeHeight - 0.02));
    return float3(field.r * _RpgGrassBend.x * mask, -down, field.g * _RpgGrassBend.x * mask);
}
#endif
