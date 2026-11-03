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

        [field: Space]
        [field: Header("Run")]
        [field: SerializeField] public float RunForwardSpeed { get; private set; } = 7f;
        [field: SerializeField] public float RunBackwardSpeed { get; private set; } = 5.5f;
        [field: SerializeField] public float RunStrafeSpeed { get; private set; } = 6f;

        [field: Space]
        [field: Header("Rotation")]
        [field: FormerlySerializedAs("RotationSpeed")]
        [field: SerializeField] public float WalkRotationSpeed { get; private set; } = 720f;
        [field: SerializeField] public float RunRotationSpeed { get; private set; } = 900f;

        [field: Space]
        [field: Header("Velocity response")]
        [field: Tooltip("How quickly movement reaches the requested walking speed, in input units per second.")]
        [field: SerializeField, Min(0.01f)] public float WalkSpeedChangeRate { get; private set; } = 3.5f;
        [field: Tooltip("How quickly movement reaches the requested running speed, in input units per second.")]
        [field: SerializeField, Min(0.01f)] public float RunSpeedChangeRate { get; private set; } = 4.5f;
        [field: Tooltip("How quickly acceleration adapts when switching between walking and running.")]
        [field: SerializeField, Min(0.01f)] public float SpeedChangeRateBlendSpeed { get; private set; } = 6f;

        [field: Space]
        [field: Header("Deceleration")]
        [field: Tooltip("How quickly walking momentum fades after movement input is released.")]
        [field: SerializeField, Min(0.01f)] public float WalkDecelerationRate { get; private set; } = 2.5f;
        [field: Tooltip("How quickly running momentum fades after movement input is released.")]
        [field: SerializeField, Min(0.01f)] public float RunDecelerationRate { get; private set; } = 3.5f;

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
    }
}
