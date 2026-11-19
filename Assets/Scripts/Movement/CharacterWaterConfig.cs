using UnityEngine;
using UnityEngine.Serialization;

namespace Movement
{
    [CreateAssetMenu(fileName = "CharacterWaterConfig", menuName = "configs/Character/Character Water Config")]
    public sealed class CharacterWaterConfig : ScriptableObject
    {
        [field: Header("Брод")]
        [field: Tooltip("Высота воды над нижней точкой CharacterController, при которой персонаж переходит в состояние брода.")]
        [field: SerializeField, Min(0f)] public float WadingEnterSubmersionHeight { get; private set; } = 0.6f;

        [field: Tooltip("Высота воды над нижней точкой CharacterController, при которой персонаж выходит из состояния брода. Значение меньше порога входа, чтобы состояние не переключалось на границе.")]
        [field: SerializeField, Min(0f)] public float WadingExitSubmersionHeight { get; private set; } = 0.5f;

        [field: Space]
        [field: Tooltip("Количество выносливости, расходуемое в секунду при движении вброд. Не зависит от расходов ходьбы, бега и спринта.")]
        [field: SerializeField, Min(0f)] public float WadingStaminaDrainPerSecond { get; private set; } = 5f;

        [field: Space]
        [field: FormerlySerializedAs("<MovementSlowdownStartSubmersionHeight>k__BackingField")]
        [field: Tooltip("Высота воды над нижней точкой CharacterController, после которой скорость начинает плавно переходить к скорости брода.")]
        [field: SerializeField, Min(0f)] public float WadingMovementSlowdownStartSubmersionHeight { get; private set; } = 0.15f;

        [field: Header("Плавание")]
        [field: Tooltip("Высота воды над нижней точкой CharacterController, при которой персонаж входит в состояние плавания. Не задаёт высоту удержания на поверхности.")]
        [field: SerializeField, Min(0f)] public float SwimEnterSubmersionHeight { get; private set; } = 1.2f;

        [field: Tooltip("Высота воды над нижней точкой CharacterController, при которой персонаж выходит из состояния плавания. Должна быть ниже порога входа и целевых глубин удержания на поверхности.")]
        [field: SerializeField, Min(0f)] public float SwimExitSubmersionHeight { get; private set; } = 0.9f;

        [field: Tooltip("Минимальная глубина trigger-объёма для мгновенного входа в Swim. Не зависит от размеров CharacterController.")]
        [field: SerializeField, Min(0f)] public float DeepWaterEntryMinimumDepth { get; private set; } = 1.8f;

        [field: Tooltip("Слои физического дна, по которым определяется возможность встать вброд.")]
        [field: SerializeField] public LayerMask WadingBottomProbeMask { get; private set; } = Physics.DefaultRaycastLayers;

        [field: Space]
        [field: FormerlySerializedAs("<SurfaceHoldSubmersionHeight>k__BackingField")]
        [field: Tooltip("Целевое расстояние от нижней точки CharacterController до поверхности воды, когда под персонажем нет дна.")]
        [field: SerializeField, Min(0f)] public float SwimSurfaceHoldSubmersionHeight { get; private set; } = 1.2f;

        [field: Tooltip("Целевое расстояние от нижней точки CharacterController до поверхности воды при плавании-беге.")]
        [field: SerializeField, Min(0f)] public float SwimRunSurfaceHoldSubmersionHeight { get; private set; } = 1.2f;

        [field: Tooltip("Целевое расстояние от нижней точки CharacterController до поверхности воды при плавании-спринте.")]
        [field: SerializeField, Min(0f)] public float SwimSprintSurfaceHoldSubmersionHeight { get; private set; } = 1.2f;

        [field: Tooltip("Скорость изменения целевой высоты на поверхности при переключении между режимами плавания.")]
        [field: SerializeField, Min(0f)] public float SwimSurfaceHoldTransitionSpeed { get; private set; } = 1f;

        [field: FormerlySerializedAs("<SurfaceHoldMaxVerticalSpeed>k__BackingField")]
        [field: Tooltip("Максимальная вертикальная скорость, с которой персонаж выравнивается до целевого положения на поверхности воды.")]
        [field: SerializeField, Min(0f)] public float SwimSurfaceHoldMaxVerticalSpeed { get; private set; } = 6f;

        [field: Header("Вход в глубокую воду")]
        [field: Tooltip("Ускорение, с которым вода выталкивает падающего персонажа, м/с². Для CharacterController это сила на единицу массы.")]
        [field: FormerlySerializedAs("<SwimFallDeceleration>k__BackingField")]
        [field: SerializeField, Min(0f)] public float SwimBuoyancyAcceleration { get; private set; } = 12f;

        [field: Tooltip("Предельная скорость всплытия при входе в глубокую воду, м/с.")]
        [field: SerializeField, Min(0f)] public float SwimBuoyancyMaxAscentSpeed { get; private set; } = 3f;

        [field: Tooltip("Максимальная скорость погружения, при которой управление возвращается игроку. При всплытии управление также возвращается.")]
        [field: SerializeField, Min(0f)] public float SwimControlRestoreMaxDownwardSpeed { get; private set; } = 0.25f;

        public float EvaluateWadingMovementSpeedBlend(float submersionHeight)
        {
            return Mathf.InverseLerp(
                WadingMovementSlowdownStartSubmersionHeight,
                WadingEnterSubmersionHeight,
                submersionHeight);
        }

        private void OnValidate()
        {
            WadingEnterSubmersionHeight = Mathf.Max(0f, WadingEnterSubmersionHeight);
            WadingExitSubmersionHeight = Mathf.Clamp(WadingExitSubmersionHeight, 0f, WadingEnterSubmersionHeight);
            WadingStaminaDrainPerSecond = Mathf.Max(0f, WadingStaminaDrainPerSecond);
            WadingMovementSlowdownStartSubmersionHeight = Mathf.Clamp(
                WadingMovementSlowdownStartSubmersionHeight,
                0f,
                WadingEnterSubmersionHeight);
            SwimEnterSubmersionHeight = Mathf.Max(0f, SwimEnterSubmersionHeight);
            SwimExitSubmersionHeight = Mathf.Clamp(
                SwimExitSubmersionHeight,
                0f,
                SwimEnterSubmersionHeight);
            DeepWaterEntryMinimumDepth = Mathf.Max(0f, DeepWaterEntryMinimumDepth);
            SwimSurfaceHoldSubmersionHeight = Mathf.Max(0f, SwimSurfaceHoldSubmersionHeight);
            SwimRunSurfaceHoldSubmersionHeight = Mathf.Max(0f, SwimRunSurfaceHoldSubmersionHeight);
            SwimSprintSurfaceHoldSubmersionHeight = Mathf.Max(0f, SwimSprintSurfaceHoldSubmersionHeight);
            SwimSurfaceHoldTransitionSpeed = Mathf.Max(0f, SwimSurfaceHoldTransitionSpeed);
            SwimSurfaceHoldMaxVerticalSpeed = Mathf.Max(0f, SwimSurfaceHoldMaxVerticalSpeed);
            SwimBuoyancyAcceleration = Mathf.Max(0f, SwimBuoyancyAcceleration);
            SwimBuoyancyMaxAscentSpeed = Mathf.Max(0f, SwimBuoyancyMaxAscentSpeed);
            SwimControlRestoreMaxDownwardSpeed = Mathf.Max(0f, SwimControlRestoreMaxDownwardSpeed);
        }
    }
}
