using UnityEngine;

namespace Gravity
{
    [CreateAssetMenu(fileName = "FallConfig", menuName = "configs/Gravity/FallConfig")]
    public sealed class FallConfig : ScriptableObject
    {
        [field: SerializeField]
        [field: Min(0f)]
        [field: Tooltip("Continuous downward-fall time after which the animator receives FallStage = 1.")]
        public float StageTwoStartTime { get; private set; } = 1f;

        [field: SerializeField]
        [field: Min(0f)]
        [field: Tooltip("Continuous downward-fall time after which the animator receives FallStage = 2 and landing deals damage.")]
        public float StageThreeStartTime { get; private set; } = 2f;

        [field: SerializeField]
        [field: Min(0f)]
        [field: Tooltip("HP removed once when a first-stage fall reaches the ground.")]
        public float StageOneLandingDamage { get; private set; }

        [field: SerializeField]
        [field: Min(0f)]
        [field: Tooltip("HP removed once when a second-stage fall reaches the ground.")]
        public float StageTwoLandingDamage { get; private set; } = 20f;

        [field: SerializeField]
        [field: Min(0f)]
        [field: Tooltip("HP removed once when a third-stage fall reaches the ground.")]
        public float StageThreeLandingDamage { get; private set; } = 50f;

        private void OnValidate()
        {
            StageTwoStartTime = Mathf.Max(0f, StageTwoStartTime);
            StageThreeStartTime = Mathf.Max(StageTwoStartTime + 0.01f, StageThreeStartTime);
            StageOneLandingDamage = Mathf.Max(0f, StageOneLandingDamage);
            StageTwoLandingDamage = Mathf.Max(0f, StageTwoLandingDamage);
            StageThreeLandingDamage = Mathf.Max(0f, StageThreeLandingDamage);
        }
    }
}
