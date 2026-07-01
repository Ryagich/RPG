using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(fileName = "HitReactionConfig", menuName = "configs/Character/HitReactionConfig")]
    public sealed class HitReactionConfig : ScriptableObject
    {
        [field: SerializeField, Min(0f)] public float DamageReactionThreshold { get; private set; } = 15f;
        [field: SerializeField, Min(0.01f)] public float DamageReactionWindow { get; private set; } = 1.2f;
        [field: SerializeField, Min(0f)] public float ReactionCooldown { get; private set; } = 0.8f;

        public static HitReactionConfig CreateDefault()
        {
            return CreateInstance<HitReactionConfig>();
        }
    }
}
