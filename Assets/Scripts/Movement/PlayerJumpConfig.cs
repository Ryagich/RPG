using UnityEngine;

namespace Movement
{
    [CreateAssetMenu(fileName = "PlayerJumpConfig", menuName = "configs/Player Movement/Player Jump Config")]
    public sealed class PlayerJumpConfig : ScriptableObject
    {
        [field: SerializeField]
        [field: Min(0.01f)]
        [field: Tooltip("Maximum rise above the take-off point, in world units.")]
        public float Height { get; private set; } = 1.5f;
    }
}
