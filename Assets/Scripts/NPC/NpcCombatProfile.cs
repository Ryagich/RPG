using UnityEngine;

namespace NPC
{
    [CreateAssetMenu(fileName = "NpcCombatProfile", menuName = "configs/NPC/Combat Profile")]
    public sealed class NpcCombatProfile : ScriptableObject
    {
        [field: Header("Baseline preferences")]
        [field: SerializeField, Range(0f, 1f)] public float Aggression { get; private set; } = 0.5f;
        [field: SerializeField, Range(0f, 1f)] public float Caution { get; private set; } = 0.5f;
        [field: SerializeField, Range(0f, 1f)] public float Unpredictability { get; private set; } = 0.35f;
        [field: SerializeField, Range(0f, 1f)] public float DistancePreference { get; private set; } = 0.5f;

        [field: Header("Situational response")]
        [field: SerializeField, Range(0f, 1f)] public float PressureResponse { get; private set; } = 0.7f;
        [field: SerializeField, Range(0f, 1f)] public float DamageCautionResponse { get; private set; } = 0.75f;
        [field: SerializeField, Range(0f, 1f)] public float LowStaminaCautionResponse { get; private set; } = 0.8f;

        [field: Header("Combat actions")]
        [field: SerializeField, Range(0f, 1f)] public float HeavyAttackPreference { get; private set; } = 0.35f;
        [field: SerializeField, Range(0f, 1f)] public float DodgePreference { get; private set; } = 0.55f;
        [field: SerializeField, Range(0f, 1f)] public float RollPreference { get; private set; } = 0.3f;
    }
}
