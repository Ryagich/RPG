using System;
using System.Collections.Generic;
using Sounds;
using UnityEngine;
using UnityEngine.Audio;
using VContainer.Unity;

namespace GameAudio
{
    /// <summary>
    /// Persistent audio service with bounded source pools. It replaces the former
    /// Instantiate/Destroy-per-sound pattern from FPS while retaining its mixer groups.
    /// </summary>
    public sealed class AudioService : IAudioService, IStartable, IDisposable
    {
        private const string VolumePreferencePrefix = "RPG.Audio.Volume.";
        private const string LegacyEffectsPreferenceKey = VolumePreferencePrefix + "Effects";
        private const int UiPoolLimit = 6;
        private const int GamePoolLimit = 20;
        private const int FootstepsPoolLimit = 16;

        private sealed class SourcePool
        {
            private readonly Transform parent;
            private readonly AudioSource prefab;
            private readonly int limit;
            private readonly List<AudioSource> sources = new();
            private int replacementIndex;

            public SourcePool(Transform parent, AudioSource prefab, int limit)
            {
                this.parent = parent;
                this.prefab = prefab;
                this.limit = limit;
            }

            public AudioSource Get()
            {
                foreach (var source in sources)
                {
                    if (source != null && !source.isPlaying)
                    {
                        return source;
                    }
                }

                if (sources.Count < limit)
                {
                    var source = prefab != null
                        ? UnityEngine.Object.Instantiate(prefab, parent)
                        : CreateFallbackSource(parent);
                    source.playOnAwake = false;
                    sources.Add(source);
                    return source;
                }

                var reusedSource = sources[replacementIndex++ % sources.Count];
                reusedSource.Stop();
                return reusedSource;
            }

            private static AudioSource CreateFallbackSource(Transform parent)
            {
                var sourceObject = new GameObject("Pooled Audio Source");
                sourceObject.transform.SetParent(parent, false);
                return sourceObject.AddComponent<AudioSource>();
            }
        }

        private readonly AudioConfig config;
        private GameObject root;
        private SourcePool uiPool;
        private SourcePool gamePool;
        private SourcePool footstepsPool;

        public static IAudioService Current { get; private set; }

        public AudioService(AudioConfig config)
        {
            this.config = config;
        }

        public void Start()
        {
            if (config == null)
            {
                Debug.LogError("AudioConfig is not assigned in ProjectLifetimeScope.");
                return;
            }

            root = new GameObject("Audio Service");
            UnityEngine.Object.DontDestroyOnLoad(root);
            uiPool = new SourcePool(root.transform, config.UiSourcePrefab, UiPoolLimit);
            gamePool = new SourcePool(root.transform, config.GameSourcePrefab, GamePoolLimit);
            footstepsPool = new SourcePool(root.transform, config.FootstepsSourcePrefab, FootstepsPoolLimit);

            foreach (var category in AudioConfig.SettingsCategories)
            {
                ApplyDecibels(category, LoadDecibels(category));
            }

            Current = this;
        }

        public void Dispose()
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }

        public void PlayUiHover() => PlayUi(config != null ? config.ButtonHoverClip : null);
        public void PlayUiClick() => PlayUi(config != null ? config.ButtonClickClip : null);

        public void PlayFootstep(Vector3 position)
        {
            if (config == null || footstepsPool == null)
            {
                return;
            }

            var origin = position + Vector3.up * config.FootstepRaycastStartHeight;
            var mask = config.FootstepRaycastMask.value == 0 ? Physics.DefaultRaycastLayers : config.FootstepRaycastMask.value;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, config.FootstepRaycastDistance, mask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var clips = config.GetFootstepClips(hit);
            var clip = GetRandomClip(clips);
            if (clip == null)
            {
                return;
            }

            var source = footstepsPool.Get();
            ConfigureSource(source, AudioMixerCategory.Game, position, spatial: true);
            source.volume = UnityEngine.Random.Range(0.88f, 1f);
            source.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            source.clip = clip;
            source.Play();
        }

        public void Play(SoundSettings settings, Vector3 position, Transform parent = null)
        {
            if (settings == null || gamePool == null)
            {
                return;
            }

            var clip = GetRandomClip(settings.Clips);
            if (clip == null)
            {
                return;
            }

            var category = settings.isUISound ? AudioMixerCategory.UI : AudioMixerCategory.Game;
            var source = settings.isUISound ? uiPool.Get() : gamePool.Get();
            ConfigureSource(source, category, position, !settings.isUISound);
            source.priority = Mathf.Clamp(Mathf.RoundToInt(settings.priority), 0, 256);
            source.volume = UnityEngine.Random.Range(settings.volume.x, settings.volume.y);
            source.pitch = UnityEngine.Random.Range(settings.pitch.x, settings.pitch.y);
            source.reverbZoneMix = settings.reverbZoneMix;
            source.minDistance = settings.MinDistance;
            source.maxDistance = Mathf.Max(settings.MinDistance, settings.MaxDistance);
            source.clip = clip;
            source.Play();
        }

        public float GetNormalizedVolume(AudioMixerCategory category)
        {
            var decibels = LoadDecibels(category);
            return Mathf.InverseLerp(AudioConfig.MinimumDecibels, AudioConfig.MaximumDecibels, decibels);
        }

        public void SetNormalizedVolume(AudioMixerCategory category, float value)
        {
            var decibels = Mathf.Lerp(AudioConfig.MinimumDecibels, AudioConfig.MaximumDecibels, Mathf.Clamp01(value));
            ApplyDecibels(category, decibels);
            PlayerPrefs.SetFloat(GetPreferenceKey(category), decibels);
            PlayerPrefs.Save();
        }

        private void PlayUi(AudioClip clip)
        {
            if (clip == null || uiPool == null)
            {
                return;
            }

            var source = uiPool.Get();
            ConfigureSource(source, AudioMixerCategory.UI, Vector3.zero, spatial: false);
            source.volume = 1f;
            source.pitch = 1f;
            source.clip = clip;
            source.Play();
        }

        private void ConfigureSource(AudioSource source, AudioMixerCategory category, Vector3 position, bool spatial)
        {
            source.Stop();
            source.transform.SetParent(root.transform, false);
            source.transform.position = position;
            source.loop = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.outputAudioMixerGroup = config.GetMixerGroup(category);
        }

        private float LoadDecibels(AudioMixerCategory category)
        {
            var defaultValue = config != null ? config.GetDefaultDecibels(category) : AudioConfig.MaximumDecibels;
            if (category == AudioMixerCategory.Game && !PlayerPrefs.HasKey(GetPreferenceKey(category)))
            {
                return PlayerPrefs.GetFloat(LegacyEffectsPreferenceKey, defaultValue);
            }

            return PlayerPrefs.GetFloat(GetPreferenceKey(category), defaultValue);
        }

        private void ApplyDecibels(AudioMixerCategory category, float value)
        {
            if (config?.Mixer == null)
            {
                return;
            }

            config.Mixer.SetFloat(config.GetExposedParameter(category), Mathf.Clamp(value, AudioConfig.MinimumDecibels, AudioConfig.MaximumDecibels));
        }

        private static string GetPreferenceKey(AudioMixerCategory category) => VolumePreferencePrefix + category;

        private static AudioClip GetRandomClip(IReadOnlyList<AudioClip> clips)
        {
            if (clips == null || clips.Count == 0)
            {
                return null;
            }

            for (var attempt = 0; attempt < clips.Count; attempt++)
            {
                var clip = clips[UnityEngine.Random.Range(0, clips.Count)];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
