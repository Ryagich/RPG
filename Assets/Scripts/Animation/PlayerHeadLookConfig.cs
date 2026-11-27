using UnityEngine;

namespace CharacterAnimation
{
    [CreateAssetMenu(fileName = "PlayerHeadLookConfig", menuName = "configs/Camera/Player Head Look Config")]
    public sealed class PlayerHeadLookConfig : ScriptableObject
    {
        [field: Header("Вес IK")]
        [field: SerializeField, Range(0f, 1f), Tooltip("Максимальная сила поворота головы. При нуле система не меняет позу, при единице голова следует за целью с полной силой.")]
        public float HeadWeight { get; private set; } = 0.75f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Встроенное ограничение Animator Look At. Большее значение сильнее удерживает голову в естественной области вокруг тела.")]
        public float ClampWeight { get; private set; } = 0.55f;
        [field: SerializeField, Min(0f), Tooltip("Скорость плавного включения IK головы в единицах веса в секунду.")]
        public float EnterWeightSpeed { get; private set; } = 8f;
        [field: SerializeField, Min(0f), Tooltip("Скорость плавного выключения IK головы в единицах веса в секунду.")]
        public float ExitWeightSpeed { get; private set; } = 12f;

        [field: Header("Ограничения поворота")]
        [field: SerializeField, Range(0f, 180f), Tooltip("Максимальный поворот головы влево или вправо относительно направления корпуса, в градусах.")]
        public float MaximumYawAngle { get; private set; } = 65f;
        [field: SerializeField, Range(0f, 90f), Tooltip("Максимальный поворот головы вверх относительно направления корпуса, в градусах.")]
        public float MaximumUpPitchAngle { get; private set; } = 28f;
        [field: SerializeField, Range(0f, 90f), Tooltip("Максимальный поворот головы вниз относительно направления корпуса, в градусах.")]
        public float MaximumDownPitchAngle { get; private set; } = 22f;

        [field: Header("Следование за камерой")]
        [field: SerializeField, Min(0f), Tooltip("Максимальная скорость поворота головы влево-вправо, в градусах в секунду. Меньшее значение создаёт заметное отставание за камерой.")]
        public float YawFollowSpeed { get; private set; } = 240f;
        [field: SerializeField, Min(0f), Tooltip("Максимальная скорость поворота головы вверх-вниз, в градусах в секунду. Настраивается отдельно от горизонтального поворота.")]
        public float PitchFollowSpeed { get; private set; } = 160f;
        [field: SerializeField, Min(0.01f), Tooltip("Расстояние от персонажа до точки на луче камеры, куда направляется взгляд. Влияет только на направление, а не на положение камеры.")]
        public float LookDistance { get; private set; } = 4f;
    }
}
