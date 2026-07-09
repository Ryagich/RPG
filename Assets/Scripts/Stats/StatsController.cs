using System;
using System.Collections.Generic;
using UniRx;
using UI;

namespace Stats
{
    public enum StatChangeSource
    {
        Manual,
        Periodic
    }

    public readonly struct StatChangeInfo
    {
        public readonly StatType StatType;
        public readonly float PreviousValue;
        public readonly float CurrentValue;
        public readonly StatChangeSource Source;

        public StatChangeInfo(StatType statType, float previousValue, float currentValue, StatChangeSource source)
        {
            StatType = statType;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            Source = source;
        }
    }

    public class StatsController
    {
        private readonly Dictionary<StatType, Stat> statsByType;
        private readonly Subject<StatChangeInfo> changed = new();

        public Stat Hp => GetStat(StatType.Hp);
        public IObservable<StatChangeInfo> Changed => changed;

        public StatsController(StatsConfig statsConfig)
        {
            statsByType = new Dictionary<StatType, Stat>
            {
                [StatType.Hp] = new Hp(statsConfig.HpStat),
                [StatType.Water] = new AdditionalPeriodicStat(statsConfig.GetAdditionalPeriodicStatConfig(StatType.Water)),
                [StatType.Food] = new AdditionalPeriodicStat(statsConfig.GetAdditionalPeriodicStatConfig(StatType.Food)),
                // Chill отвечает за сон. Механик дня/ночи и сна пока нет, поэтому другие системы
                // не используют этот стат, но экземпляр оставлен для совместимости и будущего возврата.
                [StatType.Chill] = new AdditionalPeriodicStat(statsConfig.GetAdditionalPeriodicStatConfig(StatType.Chill)),
                [StatType.Stamina] = new Stamina(statsConfig.StaminaStat),
                [StatType.PhysicalDefense] = new (statsConfig.PhysicalDefenseStat),
                [StatType.TemperatureDefense] = new (statsConfig.TemperatureDefenseStat),
                [StatType.PsiDefense] = new (statsConfig.PsiDefenseStat),
                [StatType.MagicDefense] = new (statsConfig.MagicDefenseStat)
            };
        }

        public Stat GetStat(StatType statType)
        {
            if (!statsByType.TryGetValue(statType, out var stat))
            {
                throw new ArgumentOutOfRangeException(nameof(statType), statType, null);
            }

            return stat;
        }

        public bool TryGetStat(StatType statType, out Stat stat)
        {
            return statsByType.TryGetValue(statType, out stat);
        }

        public void AddValue(StatType statType, float value, StatChangeSource source = StatChangeSource.Manual)
        {
            var stat = GetStat(statType);
            var previousValue = stat.Value.Value;
            stat.AddValue(value);
            PublishIfChanged(statType, previousValue, stat.Value.Value, source);
        }

        public void ChangeValue(StatType statType, float value, StatChangeSource source = StatChangeSource.Manual)
        {
            var stat = GetStat(statType);
            var previousValue = stat.Value.Value;
            stat.ChangeValue(value);
            PublishIfChanged(statType, previousValue, stat.Value.Value, source);
        }

        private void PublishIfChanged(StatType statType, float previousValue, float currentValue, StatChangeSource source)
        {
            if (Math.Abs(previousValue - currentValue) <= float.Epsilon)
            {
                return;
            }

            changed.OnNext(new StatChangeInfo(statType, previousValue, currentValue, source));
        }
    }
}
