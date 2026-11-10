using UnityEngine;

namespace Gravity
{
    [CreateAssetMenu(fileName = "GravityConfig", menuName = "configs/Gravity/GravityConfig")]
    public class GravityConfig : ScriptableObject
    {
        [field: SerializeField] public float Gravity { get; private set; } = 9.81f;

        [field: SerializeField]
        [field: Min(0f)]
        [field: Tooltip("Maximum gap below the CharacterController that starts falling immediately.")]
        public float FallStartGroundClearance { get; private set; } = 0.2f;

        [field: SerializeField]
        [field: Min(0f)]
        [field: Tooltip("How long a nearby but temporarily lost ground contact is tolerated before falling begins.")]
        public float FallStartGraceDuration { get; private set; } = 0.3f;

        [field: SerializeField]
        [field: Tooltip("Colliders considered by the downward ground-clearance probe.")]
        public LayerMask GroundProbeMask { get; private set; } = Physics.DefaultRaycastLayers;
    }
}
