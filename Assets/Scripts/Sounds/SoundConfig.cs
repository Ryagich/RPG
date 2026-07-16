using UnityEngine;

namespace Sounds
{
    [CreateAssetMenu(fileName = "Sound Config", menuName = "configs/Sounds/Sound")]
    public class SoundConfig : ScriptableObject
    {
        [field: SerializeField] public SoundSettings SoundSettings { get; private set; }

        public void ConfigureForProject(
            System.Collections.Generic.IEnumerable<AudioClip> clips,
            Vector2 volume,
            Vector2 pitch,
            float minDistance,
            float maxDistance)
        {
            SoundSettings = new SoundSettings
            {
                Clips = clips == null
                    ? new System.Collections.Generic.List<AudioClip>()
                    : new System.Collections.Generic.List<AudioClip>(clips),
                priority = 128f,
                volume = volume,
                pitch = pitch,
                reverbZoneMix = 1f,
                MinDistance = minDistance,
                MaxDistance = maxDistance,
                isUISound = false,
                DistanceToPlay = maxDistance,
            };
        }
    }
}
