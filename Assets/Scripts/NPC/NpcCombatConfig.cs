using UnityEngine;

namespace NPC
{
    [CreateAssetMenu(fileName = "NpcCombatConfig", menuName = "configs/NPC/NpcCombatConfig")]
    public sealed class NpcCombatConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.05f)] public float EnemyScanInterval { get; private set; } = 0.25f;
        [field: SerializeField] public bool TreatFactionlessTargetsAsHostile { get; private set; }
        [field: SerializeField, Min(0f)] public float ApproachStoppingDistance { get; private set; } = 1.6f;
        [field: SerializeField, Min(0f)] public float LastKnownReachedDistance { get; private set; } = 1.2f;
        [field: SerializeField, Min(0f)] public float LookAtLastKnownDuration { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float AttackRequestInterval { get; private set; } = 0.65f;
        [field: SerializeField, Min(0f)] public float TargetSearchRadius { get; private set; } = 18f;
        [field: SerializeField, Min(0.1f)] public float AttackStateTimeout { get; private set; } = 3f;
        [field: SerializeField, Min(0f)] public float AttackStartDistanceTolerance { get; private set; } = 0.25f;
        [field: SerializeField, Range(0f, 1f)] public float ComboAttackChance { get; private set; } = 0.55f;
        [field: SerializeField, Min(0)] public int MaxComboAttackRequests { get; private set; } = 2;
        [field: SerializeField, Min(0f)] public float ComboAttackInputDelay { get; private set; } = 0.18f;
        [field: SerializeField, Min(0.01f)] public float ComboAttackInputInterval { get; private set; } = 0.22f;
        [field: Header("Combat Decisions")]
        [field: SerializeField, Range(0f, 1f)] public float InitialCircleChance { get; private set; } = 0.25f;
        [field: SerializeField, Min(0f)] public float PostAttackImmediateAttackWeight { get; private set; } = 0.45f;
        [field: SerializeField, Min(0f)] public float PostAttackStrafeWeight { get; private set; } = 0.25f;
        [field: SerializeField, Min(0f)] public float PostAttackBackstepWeight { get; private set; } = 0.2f;
        [field: SerializeField, Min(0f)] public float PostAttackCircleWeight { get; private set; } = 0.1f;
        [field: SerializeField, Min(0f)] public float PostAttackWaitWeight { get; private set; } = 0.12f;
        [field: SerializeField, Min(0f)] public float PostAttackKeepDistanceWeight { get; private set; } = 0.18f;
        [field: Header("Combat Wait")]
        [field: SerializeField, Min(0f)] public float WaitMinDuration { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float WaitMaxDuration { get; private set; } = 1.1f;
        [field: Header("Keep Distance")]
        [field: SerializeField, Min(0f)] public float KeepDistanceMinDuration { get; private set; } = 1.2f;
        [field: SerializeField, Min(0f)] public float KeepDistanceMaxDuration { get; private set; } = 2.4f;
        [field: SerializeField, Min(0.1f)] public float KeepDistanceMinRange { get; private set; } = 2.3f;
        [field: SerializeField, Min(0.1f)] public float KeepDistanceMaxRange { get; private set; } = 3.5f;
        [field: SerializeField, Min(0.05f)] public float KeepDistanceRepositionInterval { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float KeepDistanceAttackDelay { get; private set; } = 0.35f;
        [field: SerializeField, Range(0f, 1f)] public float KeepDistanceStrafeChance { get; private set; } = 0.65f;
        [field: SerializeField, Range(0f, 75f)] public float KeepDistanceRetreatAngle { get; private set; } = 35f;
        [field: Header("Combat Movement")]
        [field: SerializeField, Min(0f)] public float CombatMoveReachedDistance { get; private set; } = 0.45f;
        [field: SerializeField, Min(0.1f)] public float CombatMoveNavMeshSampleRadius { get; private set; } = 2f;
        [field: SerializeField, Min(0.1f)] public float CombatMoveStuckTimeout { get; private set; } = 0.75f;
        [field: SerializeField, Min(0.001f)] public float CombatMoveProgressDistance { get; private set; } = 0.08f;
        [field: SerializeField, Min(0.1f)] public float StrafeMinDistance { get; private set; } = 1.2f;
        [field: SerializeField, Min(0.1f)] public float StrafeMaxDistance { get; private set; } = 2.2f;
        [field: SerializeField, Min(0.1f)] public float BackstepMinDistance { get; private set; } = 1.2f;
        [field: SerializeField, Min(0.1f)] public float BackstepMaxDistance { get; private set; } = 2.4f;
        [field: SerializeField, Min(0.1f)] public float CircleMinRadius { get; private set; } = 2.2f;
        [field: SerializeField, Min(0.1f)] public float CircleMaxRadius { get; private set; } = 3.6f;
        [field: SerializeField, Range(5f, 180f)] public float CircleMinAngle { get; private set; } = 35f;
        [field: SerializeField, Range(5f, 180f)] public float CircleMaxAngle { get; private set; } = 75f;
        [field: Header("Group Combat")]
        [field: SerializeField, Min(1)] public int MaxDirectAttackersPerTarget { get; private set; } = 4;
        [field: SerializeField, Min(0.1f)] public float DirectAttackSlotRadius { get; private set; } = 1.65f;
        [field: SerializeField, Min(0.1f)] public float QueueCircleMinRadius { get; private set; } = 3.8f;
        [field: SerializeField, Min(0.1f)] public float QueueCircleMaxRadius { get; private set; } = 5.4f;
        [field: SerializeField, Range(5f, 180f)] public float QueueCircleMinAngle { get; private set; } = 25f;
        [field: SerializeField, Range(5f, 180f)] public float QueueCircleMaxAngle { get; private set; } = 65f;
        [field: SerializeField] public bool PreventFriendlyFire { get; private set; } = true;
        [field: SerializeField, Min(0.05f)] public float FriendlyFireLaneRadius { get; private set; } = 0.65f;
        [field: SerializeField, Min(0f)] public float TargetDownWaitDuration { get; private set; } = 2f;
        [field: Header("Aggression Notification")]
        [field: SerializeField, Min(0f)] public float AggressionNotificationDelay { get; private set; } = 1.2f;
        [field: SerializeField, Min(0f)] public float AggressionNotificationRadius { get; private set; } = 12f;
        [field: Header("Flee")]
        [field: SerializeField, Min(0.1f)] public float FleeSpeedMultiplier { get; private set; } = 1.65f;
        [field: SerializeField, Min(0.5f)] public float FleeMinDistance { get; private set; } = 6f;
        [field: SerializeField, Min(0.5f)] public float FleeMaxDistance { get; private set; } = 10f;
        [field: SerializeField, Range(0f, 90f)] public float FleeAngleJitter { get; private set; } = 25f;
        [field: SerializeField, Min(1)] public int FleeSampleAttempts { get; private set; } = 8;
        [field: SerializeField, Min(0.1f)] public float FleeNavMeshSampleRadius { get; private set; } = 3f;
        [field: SerializeField, Min(0.1f)] public float FleeOpennessProbeDistance { get; private set; } = 3f;
        [field: SerializeField, Min(0f)] public float FleeOpennessWeight { get; private set; } = 20f;
        [field: SerializeField, Min(0f)] public float FleeReachedDistance { get; private set; } = 0.75f;
        [field: SerializeField, Min(0f)] public float FleeLookBackDuration { get; private set; } = 1.5f;
    }
}
