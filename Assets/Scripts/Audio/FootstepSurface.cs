using System.Collections.Generic;
using UnityEngine;

namespace GameAudio
{
    /// <summary>
    /// Optional per-object override. Normally the layer mapping in AudioConfig is enough.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootstepSurface : MonoBehaviour
    {
        [SerializeField] private List<AudioClip> clips = new();

        public IReadOnlyList<AudioClip> Clips => clips;
    }
}
