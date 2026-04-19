using UnityEngine;
using UnityEngine.UI;

namespace Stats
{
    public class StatsHolder : MonoBehaviour
    {
        [field: SerializeField] public StatHolder HPHolder { get; private set; }
        [field: SerializeField] public StatHolder WaterHolder { get; private set; }
        [field: SerializeField] public StatHolder FoodHolder { get; private set; }
        [field: SerializeField] public StatHolder ChillHolder { get; private set; }
        [field: SerializeField] public StatHolder StaminaHolder { get; private set; }
        [field: SerializeField] public Image WeightIndicator { get; private set; }

        public StatHolder GetHolder(StatType statType)
        {
            return statType switch
            {
                StatType.Hp => HPHolder,
                StatType.Water => WaterHolder,
                StatType.Food => FoodHolder,
                StatType.Chill => ChillHolder,
                StatType.Stamina => StaminaHolder,
                _ => null
            };
        }
    }
}
