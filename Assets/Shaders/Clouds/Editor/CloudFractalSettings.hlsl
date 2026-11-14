#ifndef RPG_CLOUD_FRACTAL_SETTINGS_INCLUDED
#define RPG_CLOUD_FRACTAL_SETTINGS_INCLUDED

#define RPG_CLOUD_MAX_ITERATIONS 6

struct CloudFractalSettings
{
    int iterations;
    float lacunarity;
    float persistence;
};

CloudFractalSettings MakeCloudFractalSettings(float iterations, float lacunarity, float persistence)
{
    CloudFractalSettings settings;
    settings.iterations = (int)clamp(floor(iterations + 0.5), 1.0, (float)RPG_CLOUD_MAX_ITERATIONS);
    settings.lacunarity = clamp(lacunarity, 1.0, 4.0);
    settings.persistence = saturate(persistence);
    return settings;
}

#endif
