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
        private const string VolumePreferenceSchemaKey = VolumePreferencePrefix + "Schema";
        private const int CurrentVolumePreferenceSchema = 2;
        private const string LegacyEffectsPreferenceKey = VolumePreferencePrefix + "Effects";
        private const int UiPoolLimit = 6;
        private const int GamePoolLimit = 20;
        // Step clips last up to roughly three seconds. This leaves room for overlapping
        // nearby NPC steps without crowding out UI and other gameplay voices.
        private const int FootstepsPoolLimit = 48;
        // A character prefab may contain many ragdoll colliders. Keep enough hits to
        // reach the actual floor after filtering the actor's own colliders, without
        // allocating an array for each step.
        private const int FootstepRaycastHitLimit = 64;

        private sealed class SourcePool
        {
            private Transform parent;
            private readonly AudioSource prefab;
            private readonly int limit;
            private readonly List<AudioSource> sources = new();

            public SourcePool(Transform parent, AudioSource prefab, int limit)
            {
                this.parent = parent;
                this.prefab = prefab;
                this.limit = limit;
            }

            public AudioSource Get()
            {
                sources.RemoveAll(source => source == null);

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

                // Unity uses a smaller numeric priority as more important. Reuse the
                // least important active source first, so an NPC cannot interrupt a
                // player's currently audible step when the pool is saturated.
                var reusedSource = sources[0];
                for (var index = 1; index < sources.Count; index++)
                {
                    if (sources[index].priority >= reusedSource.priority)
                    {
                        reusedSource = sources[index];
                    }
                }
                reusedSource.Stop();
                return reusedSource;
            }

            public void SetParent(Transform valueParent)
            {
                if (valueParent == null)
                {
                    return;
                }

                parent = valueParent;
                foreach (var source in sources)
                {
                    if (source != null)
                    {
                        source.transform.SetParent(parent, true);
                    }
                }
            }

            private static AudioSource CreateFallbackSource(Transform parent)
            {
                var sourceObject = new GameObject("Pooled Audio Source");
                sourceObject.transform.SetParent(parent, false);
                return sourceObject.AddComponent<AudioSource>();
            }
        }

        private readonly AudioConfig config;
        private readonly FootstepConfig footstepConfig;
        private readonly RaycastHit[] footstepRaycastHits = new RaycastHit[FootstepRaycastHitLimit];
        private GameObject root;
        private Transform worldSoundParent;
        private Transform listenerTransform;
        private SourcePool uiPool;
        private SourcePool gamePool;
        private SourcePool footstepsPool;
        private AudioSource mainMenuMusicSource;

        public AudioService(AudioConfig config, FootstepConfig footstepConfig)
        {
            this.config = config;
            this.footstepConfig = footstepConfig;
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
            // UI and ordinary game sounds have no prefab-specific settings: each pooled
            // source is configured immediately before playback. Footsteps retain their
            // dedicated prefab because it is the project-owned template for this effect.
            uiPool = new SourcePool(root.transform, null, UiPoolLimit);
            gamePool = new SourcePool(GetWorldSoundParent(), null, GamePoolLimit);
            footstepsPool = new SourcePool(GetWorldSoundParent(), config.FootstepSourcePrefab, FootstepsPoolLimit);

            MigrateVolumePreferences();
            foreach (var category in AudioConfig.SettingsCategories)
            {
                ApplyDecibels(category, LoadDecibels(category));
            }

        }

        public void Dispose()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }

        public void PlayUiHover() => PlayUi(config != null ? config.ButtonHoverClip : null);
        public void PlayUiClick() => PlayUi(config != null ? config.ButtonClickClip : null);

        public void PlayMainMenuMusic()
        {
            var clip = config != null ? config.MainMenuMusicClip : null;
            if (clip == null || root == null)
            {
                return;
            }

            if (mainMenuMusicSource == null)
            {
                var sourceObject = new GameObject("Main Menu Music");
                sourceObject.transform.SetParent(root.transform, false);
                mainMenuMusicSource = sourceObject.AddComponent<AudioSource>();
            }

            if (mainMenuMusicSource.isPlaying && mainMenuMusicSource.clip == clip)
            {
                return;
            }

            ConfigureSource(mainMenuMusicSource, AudioMixerCategory.Music, Vector3.zero, spatial: false, root.transform);
            mainMenuMusicSource.loop = true;
            mainMenuMusicSource.volume = 1f;
            mainMenuMusicSource.pitch = 1f;
            mainMenuMusicSource.clip = clip;
            mainMenuMusicSource.Play();
        }

        public void StopMainMenuMusic()
        {
            if (mainMenuMusicSource == null)
            {
                return;
            }

            mainMenuMusicSource.Stop();
            mainMenuMusicSource.clip = null;
        }

        public void SetWorldSoundParent(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            worldSoundParent = parent;
            gamePool?.SetParent(parent);
            footstepsPool?.SetParent(parent);
        }

        public void SetListenerTransform(Transform valueListenerTransform)
        {
            listenerTransform = valueListenerTransform;
        }

        public void PlayFootstep(Vector3 position, Transform actorTransform, bool isPlayerCharacter)
        {
            if (footstepConfig == null || footstepsPool == null)
            {
                return;
            }

            var origin = GetFootstepRayOrigin(position, actorTransform);
            var mask = footstepConfig.FootstepRaycastMask.value == 0
                ? Physics.DefaultRaycastLayers
                : footstepConfig.FootstepRaycastMask.value;
            var hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                footstepRaycastHits,
                footstepConfig.FootstepRaycastDistance,
                mask,
                QueryTriggerInteraction.Ignore);
            if (!TryGetFootstepSurfaceHit(hitCount, actorTransform, out var hit))
            {
                return;
            }

            var settings = footstepConfig.GetSoundSettings(hit);
            var clips = footstepConfig.GetClips(hit, settings);
            var clip = GetRandomClip(clips);
            if (clip == null || settings == null)
            {
                return;
            }

            if (!CanPlayAt(position, settings.DistanceToPlay))
            {
                return;
            }

            var source = footstepsPool.Get();
            ConfigureSource(source, AudioMixerCategory.Game, hit.point, spatial: true, GetWorldSoundParent());
            var configuredPriority = Mathf.Clamp(Mathf.RoundToInt(settings.priority), 0, 256);
            source.priority = isPlayerCharacter
                ? Mathf.Min(configuredPriority, 64)
                : Mathf.Max(configuredPriority, 160);
            source.volume = UnityEngine.Random.Range(settings.volume.x, settings.volume.y);
            source.pitch = UnityEngine.Random.Range(settings.pitch.x, settings.pitch.y);
            source.reverbZoneMix = settings.reverbZoneMix;
            source.minDistance = settings.MinDistance;
            source.maxDistance = Mathf.Max(settings.MinDistance, settings.MaxDistance);
            source.clip = clip;
            source.Play();
        }

        private Vector3 GetFootstepRayOrigin(Vector3 position, Transform actorTransform)
        {
            if (actorTransform == null)
            {
                return position + Vector3.up * footstepConfig.FootstepRaycastStartHeight;
            }

            var controller = actorTransform.GetComponent<CharacterController>();
            if (controller == null)
            {
                return position + Vector3.up * footstepConfig.FootstepRaycastStartHeight;
            }

            var localFeetPosition = controller.center - Vector3.up * (controller.height * 0.5f);
            var worldFeetPosition = controller.transform.TransformPoint(localFeetPosition);
            var clearance = Mathf.Max(0.02f, controller.skinWidth + 0.02f);
            return worldFeetPosition + Vector3.up * clearance;
        }

        private bool TryGetFootstepSurfaceHit(int hitCount, Transform actorTransform, out RaycastHit surfaceHit)
        {
            surfaceHit = default;
            var closestDistance = float.MaxValue;

            for (var index = 0; index < hitCount; index++)
            {
                var hit = footstepRaycastHits[index];
                if (hit.collider == null || IsActorCollider(hit.collider.transform, actorTransform))
                {
                    continue;
                }

                if (hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                surfaceHit = hit;
            }

            return surfaceHit.collider != null;
        }

        private static bool IsActorCollider(Transform colliderTransform, Transform actorTransform)
        {
            return actorTransform != null
                   && colliderTransform != null
                   && (colliderTransform == actorTransform || colliderTransform.IsChildOf(actorTransform));
        }

        private Transform GetWorldSoundParent()
        {
            return worldSoundParent != null ? worldSoundParent : root.transform;
        }

        private bool CanPlayAt(Vector3 position, float distanceToPlay)
        {
            return listenerTransform == null
                   || distanceToPlay <= 0f
                   || Vector3.SqrMagnitude(listenerTransform.position - position) <= distanceToPlay * distanceToPlay;
        }

        public void Play(SoundSettings settings, Vector3 position, Transform parent = null)
        {
            if (settings == null || gamePool == null)
            {
                return;
            }

            if (!settings.isUISound && !CanPlayAt(position, settings.DistanceToPlay))
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
            var sourceParent = settings.isUISound
                ? root.transform
                : parent != null ? parent : GetWorldSoundParent();
            ConfigureSource(source, category, position, !settings.isUISound, sourceParent);
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
            return DecibelsToNormalizedVolume(decibels);
        }

        public void SetNormalizedVolume(AudioMixerCategory category, float value)
        {
            var decibels = NormalizedVolumeToDecibels(value);
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
            ConfigureSource(source, AudioMixerCategory.UI, Vector3.zero, spatial: false, root.transform);
            source.volume = 1f;
            source.pitch = 1f;
            source.clip = clip;
            source.Play();
        }

        private void ConfigureSource(
            AudioSource source,
            AudioMixerCategory category,
            Vector3 position,
            bool spatial,
            Transform parent)
        {
            source.Stop();
            source.transform.SetParent(parent, false);
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

        private void MigrateVolumePreferences()
        {
            if (PlayerPrefs.GetInt(VolumePreferenceSchemaKey) >= CurrentVolumePreferenceSchema)
            {
                return;
            }

            foreach (var category in AudioConfig.SettingsCategories)
            {
                var preferenceKey = GetPreferenceKey(category);
                if (!PlayerPrefs.HasKey(preferenceKey))
                {
                    if (category == AudioMixerCategory.Game && PlayerPrefs.HasKey(LegacyEffectsPreferenceKey))
                    {
                        var legacyEffectsDecibels = PlayerPrefs.GetFloat(LegacyEffectsPreferenceKey);
                        var legacyEffectsNormalizedVolume = Mathf.InverseLerp(
                            AudioConfig.MinimumDecibels,
                            AudioConfig.MaximumDecibels,
                            legacyEffectsDecibels);
                        PlayerPrefs.SetFloat(preferenceKey, NormalizedVolumeToDecibels(legacyEffectsNormalizedVolume));
                    }

                    continue;
                }

                var legacyDecibels = PlayerPrefs.GetFloat(preferenceKey);
                var legacyNormalizedVolume = Mathf.InverseLerp(
                    AudioConfig.MinimumDecibels,
                    AudioConfig.MaximumDecibels,
                    legacyDecibels);
                PlayerPrefs.SetFloat(preferenceKey, NormalizedVolumeToDecibels(legacyNormalizedVolume));
            }

            PlayerPrefs.SetInt(VolumePreferenceSchemaKey, CurrentVolumePreferenceSchema);
            PlayerPrefs.Save();
        }

        private static float NormalizedVolumeToDecibels(float normalizedVolume)
        {
            if (normalizedVolume <= 0f)
            {
                return AudioConfig.MinimumDecibels;
            }

            return Mathf.Clamp(
                20f * Mathf.Log10(Mathf.Clamp01(normalizedVolume)),
                AudioConfig.MinimumDecibels,
                AudioConfig.MaximumDecibels);
        }

        private static float DecibelsToNormalizedVolume(float decibels)
        {
            if (decibels <= AudioConfig.MinimumDecibels)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
        }

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
