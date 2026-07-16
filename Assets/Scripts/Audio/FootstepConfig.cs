using System;
using System.Collections.Generic;
using Sounds;
using UnityEngine;

namespace GameAudio
{
    [Serializable]
    public sealed class FootstepSurfaceSettings
    {
        [SerializeField] private LayerMask layers;
        [SerializeField] private SoundConfig soundConfig;

        public bool Matches(int layer) => (layers.value & (1 << layer)) != 0;
        public SoundSettings SoundSettings => soundConfig != null ? soundConfig.SoundSettings : null;

        public FootstepSurfaceSettings(LayerMask layers, SoundConfig soundConfig)
        {
            this.layers = layers;
            this.soundConfig = soundConfig;
        }
    }

    /// <summary>
    /// Project-wide settings for footsteps. Surface layers select a standalone SoundConfig;
    /// an unmapped surface always falls back to the default sound config.
    /// </summary>
    [CreateAssetMenu(fileName = "Footstep Config", menuName = "configs/Audio/Footstep Config")]
    public sealed class FootstepConfig : ScriptableObject
    {
        [Header("Surface detection")]
        [SerializeField, Min(0.1f)] private float footstepRaycastDistance = 2.5f;
        [SerializeField, Min(0f)] private float footstepRaycastStartHeight = 0.8f;
        [SerializeField] private LayerMask footstepRaycastMask = Physics.DefaultRaycastLayers;

        [Header("Surface sound configs")]
        [SerializeField] private SoundConfig defaultSoundConfig;
        [SerializeField] private List<FootstepSurfaceSettings> footstepSurfaces = new();

        [Header("Step distances")]
        [SerializeField, Min(0.1f)] private float walkStepDistance = 1.7f;
        [SerializeField, Min(0.1f)] private float runStepDistance = 2.15f;
        [SerializeField, Min(0.1f)] private float npcStepDistance = 1.8f;

        public float FootstepRaycastDistance => footstepRaycastDistance;
        public float FootstepRaycastStartHeight => footstepRaycastStartHeight;
        public LayerMask FootstepRaycastMask => footstepRaycastMask;
        public float WalkStepDistance => walkStepDistance;
        public float RunStepDistance => runStepDistance;
        public float NpcStepDistance => npcStepDistance;

        public SoundSettings GetSoundSettings(RaycastHit hit)
        {
            var layer = hit.collider != null ? hit.collider.gameObject.layer : -1;
            var marker = hit.collider != null ? hit.collider.GetComponentInParent<FootstepSurface>() : null;
            if (marker != null && marker.TryGetSurfaceLayer(out var overriddenLayer))
            {
                layer = overriddenLayer;
            }

            foreach (var surface in footstepSurfaces)
            {
                if (surface != null && surface.Matches(layer) && surface.SoundSettings != null)
                {
                    return surface.SoundSettings;
                }
            }

            return defaultSoundConfig != null ? defaultSoundConfig.SoundSettings : null;
        }

        public IReadOnlyList<AudioClip> GetClips(RaycastHit hit, SoundSettings settings)
        {
            var marker = hit.collider != null ? hit.collider.GetComponentInParent<FootstepSurface>() : null;
            if (marker != null && marker.Clips.Count > 0)
            {
                return marker.Clips;
            }

            return settings != null ? settings.Clips : null;
        }

        public void ConfigureForProject(
            SoundConfig valueDefaultSoundConfig,
            LayerMask valueFootstepRaycastMask,
            IEnumerable<FootstepSurfaceSettings> valueFootstepSurfaces)
        {
            defaultSoundConfig = valueDefaultSoundConfig;
            footstepRaycastMask = valueFootstepRaycastMask;
            footstepSurfaces = valueFootstepSurfaces == null
                ? new List<FootstepSurfaceSettings>()
                : new List<FootstepSurfaceSettings>(valueFootstepSurfaces);
        }
    }
}
