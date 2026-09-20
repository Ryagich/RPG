using UnityEngine;
using UnityEngine.Serialization;

namespace Stats
{
    [CreateAssetMenu(fileName = "StaminaConfig", menuName = "configs/Stats/StaminaConfig")]
    public class StaminaConfig : ScriptableObject
    {
        [field: Header("Выносливость")]
        [field: SerializeField, FormerlySerializedAs("<StaminaStat>k__BackingField"), Tooltip("Основные настройки выносливости: максимум, восстановление, задержка восстановления после полного истощения и стоимость действий в бою.")]
        public Stamina Stamina { get; private set; }

        [field: Space, Header("Расход от веса")]
        [field: SerializeField, FormerlySerializedAs("<MovementDrainStartWeightPercent>k__BackingField"), Range(0f, 1f), Tooltip("Доля максимального веса, после которой начинается расход выносливости при движении. 0.8 означает 80% вместимости.")]
        public float WeightStartPercent { get; private set; } = .8f;
        [field: SerializeField, FormerlySerializedAs("<MovementDrainPerSecondAtMaximumLoad>k__BackingField"), Min(0f), Tooltip("Максимальный расход выносливости в секунду при ходьбе с полным инвентарём. Кривая веса может уменьшать это значение.")]
        public float MaxDrainPerSecond { get; private set; } = 6f;
        [field: SerializeField, FormerlySerializedAs("<MovementDrainByLoadCurve>k__BackingField"), Tooltip("Как растёт расход после порога веса. По оси X: вес от порога до 100%. По оси Y: доля от максимального расхода. Точка (1, 1) — полный расход при полном инвентаре.")]
        public AnimationCurve DrainByWeight { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [field: SerializeField, FormerlySerializedAs("<RunMovementDrainMultiplier>k__BackingField"), Min(0f), Tooltip("Во сколько раз расход от веса больше во время бега. Например, 1.25 означает на 25% больше.")]
        public float RunDrainMultiplier { get; private set; } = 1.25f;

        public float EvaluateMovementDrainPerSecond(float currentWeight, float maxWeight)
        {
            if (maxWeight <= 0f)
            {
                return 0f;
            }

            var normalizedWeight = Mathf.Clamp01(currentWeight / maxWeight);
            var startWeightPercent = Mathf.Clamp01(WeightStartPercent);
            if (normalizedWeight <= startWeightPercent)
            {
                return 0f;
            }

            var normalizedLoad = Mathf.InverseLerp(startWeightPercent, 1f, normalizedWeight);
            var curveValue = DrainByWeight != null
                ? DrainByWeight.Evaluate(normalizedLoad)
                : normalizedLoad;
            return Mathf.Max(0f, MaxDrainPerSecond) * Mathf.Max(0f, curveValue);
        }
    }
}
