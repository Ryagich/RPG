using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sounds
{
    [Serializable]
    public class SoundSettings
    {
        [field: SerializeField] public List<AudioClip> Clips;
        // [field: SerializeField] public Vector3 position;
        // [field: SerializeField] public Transform parent;
        [field: SerializeField] public float priority = 128f;
        [field: SerializeField] public Vector2 volume = new (.9f,1.1f);
        [field: SerializeField] public Vector2 pitch = new (.9f,1.1f);
        [field: SerializeField] public float reverbZoneMix = 1f;
        [field: SerializeField] public float MinDistance = 1f;
        [field: SerializeField] public float MaxDistance = 10;
        [field: SerializeField] public bool isUISound;
        [field: SerializeField] public float DistanceToPlay = 10.0f;

        public SoundSettings() { }
        // public SoundSettings(Vector3 position, Transform parent = null)
        // {
        //     this.position = position;
        //     this.parent = parent;
        // }

        public SoundSettings(SoundSettings oldSettings)
        {
            Clips = oldSettings.Clips;
            // position = oldSettings.position;
            // parent = oldSettings.parent;
            priority = oldSettings.priority;
            volume = oldSettings.volume;
            pitch = oldSettings.pitch;
            reverbZoneMix = oldSettings.reverbZoneMix;
            MinDistance = oldSettings.MinDistance;
            MaxDistance = oldSettings.MaxDistance;
            isUISound = oldSettings.isUISound;
            DistanceToPlay = oldSettings.DistanceToPlay;
        }
    }
}