using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GameAudio
{
    [Serializable]
    public sealed class FootstepSurfaceSettings
    {
        [SerializeField] private LayerMask layers;
        [SerializeField] private List<AudioClip> clips = new();

        public bool Matches(int layer) => (layers.value & (1 << layer)) != 0;
        public IReadOnlyList<AudioClip> Clips => clips;

        public FootstepSurfaceSettings(LayerMask layers, IEnumerable<AudioClip> clips)
        {
            this.layers = layers;
            this.clips = clips == null ? new List<AudioClip>() : new List<AudioClip>(clips);
        }
    }

    /// <summary>
    /// Single project-owned source of truth for mixer links, default levels and test sounds.
    /// Runtime volume changes are stored separately in PlayerPrefs, leaving this asset immutable.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "configs/Audio/Audio Config")]
    public sealed class AudioConfig : ScriptableObject
    {
        public const float MinimumDecibels = -80f;
        public const float MaximumDecibels = 0f;

        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;

        [Header("Pooled source prefabs")]
        [SerializeField] private AudioSource uiSourcePrefab;
        [SerializeField] private AudioSource gameSourcePrefab;
        [SerializeField] private AudioSource footstepsSourcePrefab;

        [Header("UI")]
        [SerializeField] private AudioClip buttonHoverClip;
        [SerializeField] private AudioClip buttonClickClip;

        [Header("Footsteps")]
        [SerializeField, Min(0.1f)] private float footstepRaycastDistance = 2.5f;
        [SerializeField, Min(0f)] private float footstepRaycastStartHeight = 0.8f;
        [SerializeField] private LayerMask footstepRaycastMask = ~0;
        [SerializeField] private List<AudioClip> defaultFootstepClips = new();
        [SerializeField] private List<FootstepSurfaceSettings> footstepSurfaces = new();
        [SerializeField, Min(0.1f)] private float walkStepDistance = 1.7f;
        [SerializeField, Min(0.1f)] private float runStepDistance = 2.15f;
        [SerializeField, Min(0.1f)] private float npcStepDistance = 1.8f;

        [Header("Default mixer levels")]
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float masterDefaultDecibels = -10f;
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float uiDefaultDecibels = -30f;
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float gameDefaultDecibels = -15f;
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float musicDefaultDecibels = -5f;

        public AudioMixer Mixer => mixer;
        public AudioSource UiSourcePrefab => uiSourcePrefab;
        public AudioSource GameSourcePrefab => gameSourcePrefab;
        public AudioSource FootstepsSourcePrefab => footstepsSourcePrefab;
        public AudioClip ButtonHoverClip => buttonHoverClip;
        public AudioClip ButtonClickClip => buttonClickClip;
        public float FootstepRaycastDistance => footstepRaycastDistance;
        public float FootstepRaycastStartHeight => footstepRaycastStartHeight;
        public LayerMask FootstepRaycastMask => footstepRaycastMask;
        public float WalkStepDistance => walkStepDistance;
        public float RunStepDistance => runStepDistance;
        public float NpcStepDistance => npcStepDistance;

        public static readonly AudioMixerCategory[] SettingsCategories =
        {
            AudioMixerCategory.Master,
            AudioMixerCategory.UI,
            AudioMixerCategory.Game,
            AudioMixerCategory.Music,
        };

        public string GetExposedParameter(AudioMixerCategory category) => $"{category}_Volume";

        public float GetDefaultDecibels(AudioMixerCategory category)
        {
            return category switch
            {
                AudioMixerCategory.Master => masterDefaultDecibels,
                AudioMixerCategory.UI => uiDefaultDecibels,
                AudioMixerCategory.Game => gameDefaultDecibels,
                AudioMixerCategory.Music => musicDefaultDecibels,
                _ => MaximumDecibels,
            };
        }

        public AudioMixerGroup GetMixerGroup(AudioMixerCategory category)
        {
            if (mixer == null)
            {
                return null;
            }

            var groups = mixer.FindMatchingGroups(category.ToString());
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }

        public IReadOnlyList<AudioClip> GetFootstepClips(RaycastHit hit)
        {
            var marker = hit.collider != null ? hit.collider.GetComponentInParent<FootstepSurface>() : null;
            if (marker != null && marker.Clips.Count > 0)
            {
                return marker.Clips;
            }

            var layer = hit.collider != null ? hit.collider.gameObject.layer : -1;
            foreach (var surface in footstepSurfaces)
            {
                if (surface != null && surface.Matches(layer) && surface.Clips.Count > 0)
                {
                    return surface.Clips;
                }
            }

            return defaultFootstepClips;
        }

        public void ConfigureForProject(
            AudioMixer valueMixer,
            AudioSource valueUiSourcePrefab,
            AudioSource valueGameSourcePrefab,
            AudioSource valueFootstepsSourcePrefab,
            AudioClip valueButtonHoverClip,
            AudioClip valueButtonClickClip,
            IEnumerable<AudioClip> valueDefaultFootstepClips,
            LayerMask valueFootstepRaycastMask,
            IEnumerable<FootstepSurfaceSettings> valueFootstepSurfaces)
        {
            mixer = valueMixer;
            uiSourcePrefab = valueUiSourcePrefab;
            gameSourcePrefab = valueGameSourcePrefab;
            footstepsSourcePrefab = valueFootstepsSourcePrefab;
            buttonHoverClip = valueButtonHoverClip;
            buttonClickClip = valueButtonClickClip;
            defaultFootstepClips = valueDefaultFootstepClips == null
                ? new List<AudioClip>()
                : new List<AudioClip>(valueDefaultFootstepClips);
            footstepRaycastMask = valueFootstepRaycastMask;
            footstepSurfaces = valueFootstepSurfaces == null
                ? new List<FootstepSurfaceSettings>()
                : new List<FootstepSurfaceSettings>(valueFootstepSurfaces);
        }
    }
}
