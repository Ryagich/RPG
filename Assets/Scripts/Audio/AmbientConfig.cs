using System;
using UnityEngine;

namespace GameAudio
{
    [Serializable]
    public sealed class AmbientCategorySettings
    {
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
        [SerializeField] private Vector2 delayRange;

        public bool TryGetRandomClip(out AudioClip clip)
        {
            clip = null;
            if (clips == null || clips.Length == 0)
            {
                return false;
            }

            for (var attempt = 0; attempt < clips.Length; attempt++)
            {
                var candidate = clips[UnityEngine.Random.Range(0, clips.Length)];
                if (candidate != null)
                {
                    clip = candidate;
                    return true;
                }
            }

            return false;
        }

        public float GetRandomDelay() => UnityEngine.Random.Range(
            Mathf.Min(delayRange.x, delayRange.y),
            Mathf.Max(delayRange.x, delayRange.y));
    }

    [CreateAssetMenu(fileName = "Ambient Config", menuName = "configs/Audio/Ambient Config")]
    public sealed class AmbientConfig : ScriptableObject
    {
        [SerializeField] private AmbientCategorySettings forest = new();
        [SerializeField] private AmbientCategorySettings birds = new();
        [SerializeField] private AmbientCategorySettings music = new();

        public AmbientCategorySettings Forest => forest;
        public AmbientCategorySettings Birds => birds;
        public AmbientCategorySettings Music => music;
    }
}
