using Sounds;
using UnityEngine;

namespace GameAudio
{
    public interface IAudioService
    {
        void PlayUiHover();
        void PlayUiClick();
        void PlayFootstep(Vector3 position);
        void Play(SoundSettings settings, Vector3 position, Transform parent = null);
        float GetNormalizedVolume(AudioMixerCategory category);
        void SetNormalizedVolume(AudioMixerCategory category, float value);
    }
}
