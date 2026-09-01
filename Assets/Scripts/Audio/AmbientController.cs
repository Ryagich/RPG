using System;
using UnityEngine;
using UnityEngine.Audio;
using VContainer.Unity;

namespace GameAudio
{
    /// <summary>Schedules independent non-spatial ambient tracks for the active gameplay scene.</summary>
    public sealed class AmbientController : IStartable, ITickable, IDisposable
    {
        private sealed class AmbientTrack
        {
            private readonly AmbientCategorySettings settings;
            private readonly AudioSource source;
            private float nextPlaybackTime;
            private bool isClipPlaying;

            public AmbientTrack(string name, AmbientCategorySettings settings, Transform parent, AudioMixerGroup mixerGroup)
            {
                this.settings = settings;
                var sourceObject = new GameObject(name);
                sourceObject.transform.SetParent(parent, false);
                source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.outputAudioMixerGroup = mixerGroup;
            }

            public void ScheduleInitialPlayback() => ScheduleNextPlayback();

            public void Tick()
            {
                if (source == null)
                {
                    return;
                }

                if (isClipPlaying)
                {
                    if (source.isPlaying)
                    {
                        return;
                    }

                    isClipPlaying = false;
                    ScheduleNextPlayback();
                }

                if (Time.unscaledTime < nextPlaybackTime)
                {
                    return;
                }

                if (!settings.TryGetRandomClip(out var clip))
                {
                    ScheduleNextPlayback();
                    return;
                }

                source.clip = clip;
                source.Play();
                isClipPlaying = true;
            }

            public void Dispose()
            {
                if (source == null)
                {
                    return;
                }

                source.Stop();
                UnityEngine.Object.Destroy(source.gameObject);
            }

            private void ScheduleNextPlayback()
            {
                nextPlaybackTime = Time.unscaledTime + settings.GetRandomDelay();
            }
        }

        private readonly AmbientConfig config;
        private readonly AudioConfig audioConfig;
        private GameObject root;
        private AmbientTrack[] tracks;

        public AmbientController(AmbientConfig config, AudioConfig audioConfig)
        {
            this.config = config;
            this.audioConfig = audioConfig;
        }

        public void Start()
        {
            if (config == null)
            {
                Debug.LogError("AmbientConfig is not assigned in ProjectLifetimeScope.");
                return;
            }

            root = new GameObject("Ambient Audio");
            var musicMixerGroup = audioConfig != null
                ? audioConfig.GetMixerGroup(AudioMixerCategory.Music)
                : null;
            tracks = new[]
            {
                new AmbientTrack("Forest Ambient", config.Forest, root.transform, musicMixerGroup),
                new AmbientTrack("Birds Ambient", config.Birds, root.transform, musicMixerGroup),
                new AmbientTrack("Music Ambient", config.Music, root.transform, musicMixerGroup),
            };

            foreach (var track in tracks)
            {
                track.ScheduleInitialPlayback();
            }
        }

        public void Tick()
        {
            if (tracks == null)
            {
                return;
            }

            foreach (var track in tracks)
            {
                track.Tick();
            }
        }

        public void Dispose()
        {
            if (tracks != null)
            {
                foreach (var track in tracks)
                {
                    track.Dispose();
                }

                tracks = null;
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }
    }
}
