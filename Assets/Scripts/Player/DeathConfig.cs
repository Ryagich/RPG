using UnityEngine;

namespace Player
{
    [CreateAssetMenu(fileName = "DeathConfig", menuName = "configs/Character/DeathConfig")]
    public sealed class DeathConfig : ScriptableObject
    {
        [field: SerializeField]
        [field: Tooltip("If enabled, player death is blocked. Changing this after death does not revive the player.")]
        public bool CannotDie { get; private set; }
    }
}
