using System;
using UnityEngine;

namespace EditorTools
{
    public enum CloudBakeShape
    {
        Faceted = 0,
        Rounded = 1,
        Realistic = 3
    }

    [Serializable]
    public sealed class CloudLayerBakeSettings
    {
        public CloudBakeShape shape = CloudBakeShape.Rounded;
        [Range(1, 6)] public int iterations = 3;
        [Range(1f, 4f)] public float lacunarity = 2.03f;
        [Range(0f, 1f)] public float persistence = 0.5f;
        [Range(0f, 1f)] public float detail;
        [Range(128, 4096)] public int resolution = 1024;
        [Min(0.01f)] public float period = 32f;
    }

    [CreateAssetMenu(menuName = "Rendering/Cloud Bake Settings", fileName = "CloudBakeSettings")]
    public sealed class CloudBakeSettings : ScriptableObject
    {
        public Material material;
        public string outputFolder = "Assets/Materials";
        public CloudLayerBakeSettings near = new CloudLayerBakeSettings();
        public CloudLayerBakeSettings distant = new CloudLayerBakeSettings();
    }

    internal readonly struct CloudFieldSettings
    {
        public readonly CloudBakeShape Shape;
        public readonly int Iterations;
        public readonly float Lacunarity;
        public readonly float Persistence;
        public readonly float Detail;
        public readonly int Resolution;
        public readonly float Period;

        public CloudFieldSettings(CloudLayerBakeSettings source)
        {
            Shape = source.shape;
            Iterations = Mathf.Clamp(source.iterations, 1, 6);
            Lacunarity = Mathf.Clamp(source.lacunarity, 1f, 4f);
            Persistence = Mathf.Clamp01(source.persistence);
            Detail = Mathf.Clamp01(source.detail) * 0.5f;
            Resolution = Mathf.Clamp(source.resolution, 128, 4096);
            Period = Mathf.Max(0.01f, source.period);
        }

        // Period, surface type, completed bake, reserved.
        public Vector4 Parameters => new Vector4(Period,
            Shape == CloudBakeShape.Rounded || Shape == CloudBakeShape.Realistic ? 1f : 0f, 1f, 0f);
    }
}
