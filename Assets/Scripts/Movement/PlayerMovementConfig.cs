using UnityEngine;
using UnityEngine.Serialization;

namespace Movement
{
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "configs/Player Movement/PlayerMovementConfig")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [field: Header("Walk")]
        [field: SerializeField] public float ForwardSpeed { get; private set; } = 5f;
        [field: SerializeField] public float BackwardSpeed { get; private set; } = 4f;
        [field: SerializeField] public float StrafeSpeed { get; private set; } = 4.5f;
        [field: Tooltip("Скорость разгона CurrentVelocity при ходьбе.")]
        [field: SerializeField, Min(0.01f)] public float WalkSpeedChangeRate { get; private set; } = 3.5f;
        [field: Tooltip("Скорость затухания CurrentVelocity после отпускания ввода при ходьбе.")]
        [field: SerializeField, Min(0.01f)] public float WalkDecelerationRate { get; private set; } = 2.5f;

        [field: Space]
        [field: Header("Run")]
        [field: SerializeField] public float RunForwardSpeed { get; private set; } = 7f;
        [field: SerializeField] public float RunBackwardSpeed { get; private set; } = 5.5f;
        [field: SerializeField] public float RunStrafeSpeed { get; private set; } = 6f;
        [field: Tooltip("Скорость разгона CurrentVelocity при беге.")]
        [field: SerializeField, Min(0.01f)] public float RunSpeedChangeRate { get; private set; } = 4.5f;
        [field: Tooltip("Скорость затухания CurrentVelocity после отпускания ввода при беге.")]
        [field: SerializeField, Min(0.01f)] public float RunDecelerationRate { get; private set; } = 3.5f;

        [field: Space]
        [field: Header("Sprint")]
        [field: Tooltip("Sprint only moves forward; lateral input rotates the character without changing this speed.")]
        [field: SerializeField, Min(0f)] public float SprintForwardSpeed { get; private set; } = 9f;
        [field: SerializeField, Min(0.01f)] public float SprintSpeedChangeRate { get; private set; } = 2.5f;
        [field: Tooltip("Скорость затухания CurrentVelocity после отпускания ввода при спринте.")]
        [field: SerializeField, Min(0.01f)] public float SprintDecelerationRate { get; private set; } = 4f;
        [field: SerializeField, Min(0f)] public float SprintRotationSpeed { get; private set; } = 360f;

        [field: Space]
        [field: Header("Брод")]
        [field: Tooltip("Скорость движения вперёд в состоянии брода.")]
        [field: SerializeField, Min(0f)] public float WadingForwardSpeed { get; private set; } = 1.5f;
        [field: Tooltip("Скорость движения назад в состоянии брода.")]
        [field: SerializeField, Min(0f)] public float WadingBackwardSpeed { get; private set; } = 0.7f;
        [field: Tooltip("Скорость движения в сторону в состоянии брода.")]
        [field: SerializeField, Min(0f)] public float WadingStrafeSpeed { get; private set; } = 1f;
        [field: Tooltip("Скорость разгона CurrentVelocity в состоянии брода.")]
        [field: SerializeField, Min(0.01f)] public float WadingSpeedChangeRate { get; private set; } = 1.25f;
        [field: Tooltip("Скорость затухания CurrentVelocity после отпускания ввода в состоянии брода.")]
        [field: SerializeField, Min(0.01f)] public float WadingDecelerationRate { get; private set; } = 0.89285713f;

        [field: Space]
        [field: Header("Плавание")]
        [field: Tooltip("Скорость движения вперёд при плавании на поверхности воды.")]
        [field: SerializeField, Min(0f)] public float SwimForwardSpeed { get; private set; } = 4.2f;
        [field: Tooltip("Скорость движения назад при плавании на поверхности воды.")]
        [field: SerializeField, Min(0f)] public float SwimBackwardSpeed { get; private set; } = 1.9f;
        [field: Tooltip("Скорость движения в сторону при плавании на поверхности воды.")]
        [field: SerializeField, Min(0f)] public float SwimStrafeSpeed { get; private set; } = 2.8f;
        [field: Tooltip("Скорость разгона CurrentVelocity при плавании на поверхности воды.")]
        [field: SerializeField, Min(0.01f)] public float SwimSpeedChangeRate { get; private set; } = 3.5f;
        [field: Tooltip("Скорость затухания CurrentVelocity после отпускания ввода при плавании на поверхности воды.")]
        [field: SerializeField, Min(0.01f)] public float SwimDecelerationRate { get; private set; } = 0.77380955f;

        [field: Space]
        [field: Header("Плавание-бег")]
        [field: Tooltip("Скорость движения вперёд при плавании-беге.")]
        [field: SerializeField, Min(0f)] public float SwimRunForwardSpeed { get; private set; } = 2.1666667f;
        [field: Tooltip("Скорость движения в сторону при плавании-беге.")]
        [field: SerializeField, Min(0f)] public float SwimRunStrafeSpeed { get; private set; } = 1.3333334f;
        [field: Tooltip("Скорость разгона CurrentVelocity при плавании-беге.")]
        [field: SerializeField, Min(0.01f)] public float SwimRunSpeedChangeRate { get; private set; } = 1.8333334f;
        [field: Tooltip("Скорость затухания CurrentVelocity после отпускания ввода при плавании-беге.")]
        [field: SerializeField, Min(0.01f)] public float SwimRunDecelerationRate { get; private set; } = 1.25f;
        [field: Tooltip("Скорость вперёд, добавляемая к чистому боковому движению при плавании-беге.")]
        [field: SerializeField, Min(0f)] public float SwimRunSidewaysForwardSpeed { get; private set; } = 0.5f;
        [field: Tooltip("Angular speed while swim-running with purely lateral movement.")]
        [field: SerializeField, Min(0f)] public float SwimRunSidewaysRotationSpeed { get; private set; } = 360f;

        [field: Space]
        [field: Header("Плавание-спринт")]
        [field: Tooltip("Скорость движения вперёд при плавании-спринте.")]
        [field: SerializeField, Min(0f)] public float SwimSprintForwardSpeed { get; private set; } = 2.6f;
        [field: Tooltip("Скорость движения в сторону при плавании-спринте.")]
        [field: SerializeField, Min(0f)] public float SwimSprintStrafeSpeed { get; private set; } = 1.55f;
        [field: Tooltip("Скорость разгона CurrentVelocity при плавании-спринте.")]
        [field: SerializeField, Min(0.01f)] public float SwimSprintSpeedChangeRate { get; private set; } = 1f;
        [field: Tooltip("Скорость затухания CurrentVelocity после отпускания ввода при плавании-спринте.")]
        [field: SerializeField, Min(0.01f)] public float SwimSprintDecelerationRate { get; private set; } = 0.78f;
        [field: Tooltip("Скорость вперёд, добавляемая к чистому боковому движению при плавании-спринте.")]
        [field: SerializeField, Min(0f)] public float SwimSprintSidewaysForwardSpeed { get; private set; } = 0.5f;
        [field: Tooltip("Angular speed while swim-sprinting with purely lateral movement.")]
        [field: SerializeField, Min(0f)] public float SwimSprintSidewaysRotationSpeed { get; private set; } = 360f;

        [field: Space]
        [field: Header("Rotation")]
        [field: FormerlySerializedAs("RotationSpeed")]
        [field: SerializeField] public float WalkRotationSpeed { get; private set; } = 720f;
        [field: SerializeField] public float RunRotationSpeed { get; private set; } = 900f;

        [field: Space]
        [field: Header("Плавность смены режима")]
        [field: Tooltip("Скорость, с которой разгон CurrentVelocity перестраивается при смене режима перемещения.")]
        [field: SerializeField, Min(0.01f)] public float SpeedChangeRateBlendSpeed { get; private set; } = 6f;

        [field: Space]
        [field: Header("Weight")]
        [field: SerializeField, Min(0f)] public float WeightSpeedPenaltyMultiplier { get; private set; } = 3f;
        [field: SerializeField] public AnimationCurve WeightSpeedPenaltyCurve { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public float EvaluateWeightSpeedPenalty(float normalizedOverload)
        {
            var curveValue = WeightSpeedPenaltyCurve != null
                ? WeightSpeedPenaltyCurve.Evaluate(Mathf.Clamp01(normalizedOverload))
                : Mathf.Clamp01(normalizedOverload);
            return Mathf.Max(0f, curveValue);
        }

        [field: Space]
        [field: Header("Evasion root motion")]
        [field: SerializeField, Min(0f)] public float DodgeRootMotionMultiplier { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float RollRootMotionMultiplier { get; private set; } = 3f;

        [field: Space]
        [field: Header("Buffered combat requests")]
        [field: Tooltip("How long an attack, dodge, or roll request made during another full-body action remains valid.")]
        [field: SerializeField, Min(0f)] public float BufferedCombatRequestLifetime { get; private set; } = 0.5f;
    }
}
