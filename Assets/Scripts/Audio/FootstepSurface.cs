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
        [Tooltip("Optional layer mapping for this object and all of its children. Use one Footstep layer.")]
        [SerializeField] private LayerMask surfaceLayerOverride;
        [SerializeField] private List<AudioClip> clips = new();

        public IReadOnlyList<AudioClip> Clips => clips;

        public bool TryGetSurfaceLayer(out int layer)
        {
            var mask = surfaceLayerOverride.value;
            for (var index = 0; index < 32; index++)
            {
                if ((mask & (1 << index)) != 0)
                {
                    layer = index;
                    return true;
                }
            }

            layer = -1;
            return false;
        }
    }
}
