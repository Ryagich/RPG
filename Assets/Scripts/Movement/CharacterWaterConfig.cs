using UnityEngine;

namespace Movement
{
    [CreateAssetMenu(fileName = "CharacterWaterConfig", menuName = "configs/Character/Character Water Config")]
    public sealed class CharacterWaterConfig : ScriptableObject
    {
        [field: Header("Wading")]
        [field: Tooltip("Высота воды от нижней точки CharacterController, при которой персонаж входит в Wading.")]
        [field: SerializeField, Min(0f)] public float WadingEnterSubmersionHeight { get; private set; } = 0.6f;

        [field: Tooltip("Высота воды для выхода из Wading. Меньше порога входа, чтобы состояние не мигало на границе.")]
        [field: SerializeField, Min(0f)] public float WadingExitSubmersionHeight { get; private set; } = 0.5f;

        [field: Space]
        [field: Tooltip("Количество выносливости, расходуемое в секунду при движении в состоянии Wading. Не зависит от расходов ходьбы, бега и спринта.")]
        [field: SerializeField, Min(0f)] public float WadingStaminaDrainPerSecond { get; private set; } = 5f;

        [field: Header("Замедление движения")]
        [field: Tooltip("Высота воды от нижней точки CharacterController, после которой вода считается выше ступней и начинает замедлять движение.")]
        [field: SerializeField, Min(0f)] public float MovementSlowdownStartSubmersionHeight { get; private set; } = 0.15f;

        [field: Tooltip("Высота воды от нижней точки CharacterController, при которой достигается минимальная скорость движения.")]
        [field: SerializeField, Min(0f)] public float MovementSlowdownFullSubmersionHeight { get; private set; } = 1.2f;

        [field: Tooltip("Наименьший множитель скорости при полном погружении в воду. 1 — без замедления, 0 — движение остановлено.")]
        [field: SerializeField, Range(0f, 1f)] public float MinimumMovementSpeedMultiplier { get; private set; } = 0.35f;

        [field: Tooltip("Скорость, с которой фактическая скорость персонажа подстраивается под изменение глубины воды.")]
        [field: SerializeField, Min(0f)] public float MovementSpeedMultiplierChangeRate { get; private set; } = 2.5f;

        public float EvaluateMovementSpeedMultiplier(float submersionHeight)
        {
            var slowdownStartHeight = MovementSlowdownStartSubmersionHeight;
            var slowdownFullHeight = Mathf.Max(
                slowdownStartHeight,
                MovementSlowdownFullSubmersionHeight);
            if (submersionHeight <= slowdownStartHeight)
            {
                return 1f;
            }

            if (slowdownFullHeight <= slowdownStartHeight)
            {
                return MinimumMovementSpeedMultiplier;
            }

            var slowdownProgress = Mathf.InverseLerp(
                slowdownStartHeight,
                slowdownFullHeight,
                submersionHeight);
            return Mathf.Lerp(1f, MinimumMovementSpeedMultiplier, slowdownProgress);
        }

        private void OnValidate()
        {
            WadingEnterSubmersionHeight = Mathf.Max(0f, WadingEnterSubmersionHeight);
            WadingExitSubmersionHeight = Mathf.Clamp(WadingExitSubmersionHeight, 0f, WadingEnterSubmersionHeight);
            WadingStaminaDrainPerSecond = Mathf.Max(0f, WadingStaminaDrainPerSecond);
            MovementSlowdownStartSubmersionHeight = Mathf.Max(0f, MovementSlowdownStartSubmersionHeight);
            MovementSlowdownFullSubmersionHeight = Mathf.Max(
                MovementSlowdownStartSubmersionHeight,
                MovementSlowdownFullSubmersionHeight);
            MinimumMovementSpeedMultiplier = Mathf.Clamp01(MinimumMovementSpeedMultiplier);
            MovementSpeedMultiplierChangeRate = Mathf.Max(0f, MovementSpeedMultiplierChangeRate);
        }
    }
}
