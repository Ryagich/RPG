using System;
using System.Collections.Generic;
using UI;

namespace Stats
{
    public class StatFillers : IDisposable
    {
        private readonly Dictionary<StatType, StatFiller> fillersByType;

        public StatFillers(StatsConfig statsConfig, StatsController statsController)
        {
            fillersByType = new Dictionary<StatType, StatFiller>
            {
                [StatType.Hp] = new StatFiller(StatType.Hp, statsConfig, statsController),
                [StatType.Water] = new StatFiller(StatType.Water, statsConfig, statsController),
                [StatType.Food] = new StatFiller(StatType.Food, statsConfig, statsController),
                [StatType.Chill] = new StatFiller(StatType.Chill, statsConfig, statsController),
                [StatType.Stamina] = new StatFiller(StatType.Stamina, statsConfig, statsController)
            };
        }

        public StatFiller Get(StatType statType)
        {
            if (!fillersByType.TryGetValue(statType, out var filler))
            {
                throw new ArgumentOutOfRangeException(nameof(statType), statType, null);
            }

            return filler;
        }

        public bool TryGet(StatType statType, out StatFiller filler)
        {
            return fillersByType.TryGetValue(statType, out filler);
        }

        public void Dispose()
        {
            foreach (var filler in fillersByType.Values)
            {
                filler.Dispose();
            }
        }
    }
}
