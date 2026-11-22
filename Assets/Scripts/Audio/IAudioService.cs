using Sounds;
using UnityEngine;
using UnityEngine.Audio;

namespace GameAudio
{
    public interface IAudioService
    {
        void PlayUiHover();
        void PlayUiClick();
        void PlayMainMenuMusic();
        void StopMainMenuMusic();
        void SetWorldSoundParent(Transform parent);
        void SetListenerTransform(Transform listenerTransform);
        void PlayFootstep(Vector3 position, Transform actorTransform, bool isPlayerCharacter);
        void Play(SoundSettings settings, Vector3 position, Transform parent = null);
        void PlayResource(AudioResource resource, Vector3 position);
        float GetNormalizedVolume(AudioMixerCategory category);
        void SetNormalizedVolume(AudioMixerCategory category, float value);
    }
}
