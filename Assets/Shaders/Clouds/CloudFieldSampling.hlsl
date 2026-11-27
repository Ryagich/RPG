#ifndef RPG_CLOUD_FIELD_SAMPLING_INCLUDED
#define RPG_CLOUD_FIELD_SAMPLING_INCLUDED

float CloudCoverageMask(float density, float2 limits)
{
    float width = limits.y - limits.x;
    [branch] if (width > 0.0001)
        return saturate((density - limits.x) / width);
    // Equal bounds need antialiasing instead of division by zero.
    float edgeAA = max(fwidth(density), 0.0001);
    return smoothstep(limits.x - edgeAA, limits.y + edgeAA, density);
}

// RG = density/tint, BA = signed density gradient, before surface conversion.
float4 CloudFieldToSurface(float4 field, float threshold, float rounded)
{
    field.zw *= lerp(1.0, 0.5 * rsqrt(max(field.x - threshold, 0.02)), rounded);
    return field;
}

#endif
