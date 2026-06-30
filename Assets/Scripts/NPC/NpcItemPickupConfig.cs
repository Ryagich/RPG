using UnityEngine;

namespace NPC
{
    [CreateAssetMenu(fileName = "NpcItemPickupConfig", menuName = "configs/NPC/NpcItemPickupConfig")]
    public sealed class NpcItemPickupConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.05f)] public float ScanInterval { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float PickupDelay { get; private set; } = 2f;
        [field: SerializeField, Min(0.05f)] public float InteractionRadius { get; private set; } = 1.2f;
        [field: SerializeField, Min(0.05f)] public float HomeReachedDistance { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float EtaWinMarginSeconds { get; private set; } = 0.5f;
        [field: SerializeField, Min(0f)] public float WeightPenalty { get; private set; } = 0.25f;
        [field: SerializeField, Min(0f)] public float SizePenalty { get; private set; } = 0.1f;
    }
}
