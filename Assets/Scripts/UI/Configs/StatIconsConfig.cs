using Stats;
using UnityEngine;

namespace UI.Configs
{
    [CreateAssetMenu(fileName = "Stat Icons Config", menuName = "configs/UI/Stat Icons")]
    public class StatIconsConfig : ScriptableObject
    {
        [field: SerializeField] public Sprite HpStat { get; private set; }
        [field: SerializeField] public Sprite WaterStat { get; private set; }
        [field: SerializeField] public Sprite FoodStat { get; private set; }
        [field: SerializeField] public Sprite ChillStat { get; private set; }
        [field: SerializeField] public Sprite StaminaStat { get; private set; }
        [field: SerializeField] public Sprite PhysicalDefenseStat { get; private set; }
        [field: SerializeField] public Sprite TemperatureDefenseStat { get; private set; }
        [field: SerializeField] public Sprite PsiDefenseStat { get; private set; }
        [field: SerializeField] public Sprite MagicDefenseStat { get; private set; }
    }
}