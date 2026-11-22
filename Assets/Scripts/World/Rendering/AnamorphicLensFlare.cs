using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace World.Rendering
{
    [Serializable, VolumeComponentMenu("Post-processing/Anamorphic Bloom and Flares")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class AnamorphicLensFlare : VolumeComponent, IPostProcessComponent
    {
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        public MinFloatParameter bloomIntensity = new MinFloatParameter(0.2f, 0f);
        public MinFloatParameter flareIntensity = new MinFloatParameter(0.08f, 0f);
        [Tooltip("HDR brightness threshold, before tone mapping and post exposure.")]
        public MinFloatParameter threshold = new MinFloatParameter(2f, 0f);
        public MinFloatParameter clamp = new MinFloatParameter(32f, 0.01f);
        [Header("Screen falloff")]
        [Tooltip("Width of the smooth fade at the screen boundary. Zero disables the fade.")]
        public ClampedFloatParameter edgeFalloff = new ClampedFloatParameter(0.2f, 0f, 1f);
        [Tooltip("0: fade from the rectangular screen edges. 1: radial fade from the screen center.")]
        public ClampedFloatParameter radialFalloff = new ClampedFloatParameter(0f, 0f, 1f);

        [Header("Bloom directions")]
        [Tooltip("Number of evenly spaced streak axes; each axis has two opposing rays.")]
        public ClampedIntParameter directionCount = new ClampedIntParameter(6, 1, 8);
        [Tooltip("Global streak length relative to screen height, multiplied by each direction's stretch.")]
        public ClampedFloatParameter stretch = new ClampedFloatParameter(0.65f, 0.01f, 2f);
        public ClampedFloatParameter rotation = new ClampedFloatParameter(0f, -180f, 180f);
        public BloomDirectionParameter direction1 = new BloomDirectionParameter();
        public BloomDirectionParameter direction2 = new BloomDirectionParameter();
        public BloomDirectionParameter direction3 = new BloomDirectionParameter();
        public BloomDirectionParameter direction4 = new BloomDirectionParameter();
        public BloomDirectionParameter direction5 = new BloomDirectionParameter();
        public BloomDirectionParameter direction6 = new BloomDirectionParameter();
        public BloomDirectionParameter direction7 = new BloomDirectionParameter();
        public BloomDirectionParameter direction8 = new BloomDirectionParameter();

        [Header("Flares")]
        public ClampedIntParameter ghostCount = new ClampedIntParameter(4, 1, 8);
        public ClampedFloatParameter ghostSpacing = new ClampedFloatParameter(0.45f, 0f, 2f);
        public ClampedFloatParameter chromaticDispersion = new ClampedFloatParameter(0.005f, 0f, 0.05f);
        public ColorParameter tint = new ColorParameter(new Color(0.65f, 0.8f, 1f), false, false, true);

        public Vector2 GetDirection(int index)
        {
            switch (index)
            {
                case 0: return direction1.value;
                case 1: return direction2.value;
                case 2: return direction3.value;
                case 3: return direction4.value;
                case 4: return direction5.value;
                case 5: return direction6.value;
                case 6: return direction7.value;
                case 7: return direction8.value;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public bool HasBloom()
        {
            if (bloomIntensity.value <= 0f) return false;
            for (int i = 0; i < directionCount.value; i++)
                if (GetDirection(i).x > 0f) return true;
            return false;
        }

        public bool IsActive() => intensity.value > 0f && (HasBloom() || flareIntensity.value > 0f);
    }
}
