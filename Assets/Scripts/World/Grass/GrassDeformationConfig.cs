using UnityEngine;

namespace World.Grass
{
    [CreateAssetMenu(menuName = "RPG/World/Grass Deformation")]
    public sealed class GrassDeformationConfig : ScriptableObject
    {
        [field: SerializeField, Tooltip("Включает пригибание травы игроком и NPC. Изменение применяется во время игры; при отключении карта освобождается, ветер сохраняется.")]
        public bool Enabled { get; private set; } = true;
        [field: SerializeField] public ComputeShader Compute { get; private set; }
        [field: SerializeField] public Shader GrassShader { get; private set; }
        [field: SerializeField, Min(32)] public int Resolution { get; private set; } = 256;
        [field: SerializeField, Min(4f)] public float WorldSize { get; private set; } = 24f;
        [field: SerializeField, Range(1, 64)] public int EntitySlots { get; private set; } = 16;
        [field: SerializeField, Min(0.1f), Tooltip("Множитель горизонтального радиуса капсулы персонажа для отпечатка игрока и NPC.")]
        public float SourceRadiusMultiplier { get; private set; } = 1.6f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Сила прижима игроком и NPC. Ноль отключает новые отпечатки; оставленные следы продолжают восстанавливаться.")]
        public float SourceStrength { get; private set; } = 1f;
        [field: SerializeField, Min(0.05f), Tooltip("За сколько секунд оставленный след теряет половину силы. Восстановление продолжается и после выхода персонажа из области касания.")]
        public float RecoveryHalfLife { get; private set; } = 1.5f;
        [field: SerializeField, Min(0.05f), Tooltip("Скорость усиления прижима при новом касании. Не влияет на восстановление оставленного следа.")]
        public float PressSpeed { get; private set; } = 18f;
        [field: SerializeField, Min(0f)] public float SelectionHysteresis { get; private set; } = 0.75f;
        [field: SerializeField, Min(0.1f)] public float MaxSweepDistance { get; private set; } = 3f;
        [field: SerializeField, Min(0f)] public float HorizontalBend { get; private set; } = 0.45f;
        [field: SerializeField, Min(0f)] public float DownwardBend { get; private set; } = 0.65f;
        [field: SerializeField, Min(0.05f)] public float HeightTolerance { get; private set; } = 0.3f;
        [field: SerializeField, Min(0.1f)] public float MaxBladeHeight { get; private set; } = 2f;
    }
}
