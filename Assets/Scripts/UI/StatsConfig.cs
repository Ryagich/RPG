using Stats;
using UnityEngine;

namespace UI
{
    [CreateAssetMenu(fileName = "StatsConfig", menuName = "configs/Stats/StatsConfig")]
    public class StatsConfig : ScriptableObject
    {
        [Header("Stats Settings")] 
        [field: SerializeField] public Hp HpStat { get; private set; }
        [field: SerializeField] public AdditionalPeriodicStat WaterStat { get; private set; }
        [field: SerializeField] public AdditionalPeriodicStat FoodStat { get; private set; }
        [field: SerializeField] public AdditionalPeriodicStat ChillStat { get; private set; }
        [field: SerializeField] public Stamina StaminaStat { get; private set; }

        [field: Space, Header("Periodic Change")]
        [field: SerializeField, Min(.0f)] public float PeriodicChangeIntervalSeconds { get; private set; } = 1f;

        public Stat GetStatConfig(StatType statType)
        {
            return statType switch
            {
                StatType.Hp => HpStat,
                StatType.Water => WaterStat,
                StatType.Food => FoodStat,
                StatType.Chill => ChillStat,
                StatType.Stamina => StaminaStat,
                _ => throw new System.ArgumentOutOfRangeException(nameof(statType), statType, null)
            };
        }

        public PeriodicStat GetPeriodicStatConfig(StatType statType)
        {
            return statType switch
            {
                StatType.Hp => HpStat,
                StatType.Water => WaterStat,
                StatType.Food => FoodStat,
                StatType.Chill => ChillStat,
                StatType.Stamina => StaminaStat,
                _ => throw new System.ArgumentOutOfRangeException(nameof(statType), statType, null)
            };
        }

        public AdditionalPeriodicStat GetAdditionalPeriodicStatConfig(StatType statType)
        {
            return statType switch
            {
                StatType.Water => WaterStat,
                StatType.Food => FoodStat,
                StatType.Chill => ChillStat,
                _ => throw new System.ArgumentOutOfRangeException(nameof(statType), statType, null)
            };
        }
        
        [field: Space, Header("Bar")]
        [field: SerializeField, Min(0)] public float FillSpeed  { get; private set; } = .5f;
        [field: SerializeField, Min(0)] public float FllLerpSpeed { get; private set; } = .1f;

        [field: Space, Header("Visibility")]
        [field: SerializeField, Min(0f)] public float FadeOutTime { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float ShowTime { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float AlphaRestoreTime { get; private set; } = 1f;
        
        [field: Space, Header("Heart")]
        [field: SerializeField, Min(0)] public int MinHeartbeat { get; private set; } = 80;
        [field: SerializeField, Min(0)] public int MaxHeartbeat { get; private set; } = 190;
        [field: SerializeField] public int Sharpness { get; private set; } = 3; 
        [field: SerializeField] public float HeartDefSize { get; private set; } = .9f;
        [field: SerializeField] public float HeartMaxSize { get; private set; } = 1f;
        
        [field: Space, Header("Bars Colors")]
        [field: SerializeField] public Color HpFullColor { get; private set; } = Color.white;
        [field: SerializeField] public Color HpRecoveryColor { get; private set; } = Color.green;
        [field: SerializeField] public Color HpDecreaseColor { get; private set; } = Color.red;
        [field: SerializeField] public Color Warning { get; private set; } = Color.yellow;
    }
}
