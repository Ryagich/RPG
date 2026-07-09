using UI;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Stats
{
    public class StatsPeriodicChanger : IStartable, ITickable, System.IDisposable
    {
        // Chill отвечает за сон. Пока нет механик дня/ночи и сна, он выключен из периодики,
        // но сам стат остаётся в проекте для будущего возврата.
        private static readonly StatType[] AdditionalStatTypes = { StatType.Water, StatType.Food };

        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly System.IDisposable hpChangeSubscription;

        private float elapsedTime;
        private float hpRegenBlockedRemainingTime;
        private bool isStarted;

        public StatsPeriodicChanger(StatsConfig statsConfig, StatsController statsController)
        {
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            hpChangeSubscription = statsController.Changed.Subscribe(changeInfo => OnStatChanged(changeInfo));
        }

        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            elapsedTime = 0f;
            hpRegenBlockedRemainingTime = 0f;
        }

        public void Tick()
        {
            if (!isStarted)
            {
                return;
            }

            hpRegenBlockedRemainingTime = Mathf.Max(0f, hpRegenBlockedRemainingTime - Time.deltaTime);

            var interval = Mathf.Max(statsConfig.PeriodicChangeIntervalSeconds, float.Epsilon);
            elapsedTime += Time.deltaTime;

            while (elapsedTime >= interval)
            {
                elapsedTime -= interval;
                ApplyPeriodicChanges();
            }
        }

        private void ApplyPeriodicChanges()
        {
            ApplyPeriodicChange(StatType.Water);
            ApplyPeriodicChange(StatType.Food);

            ApplyHpPeriodicChange();
            ApplyEmptyAdditionalStatDamage();
        }

        private void ApplyHpPeriodicChange()
        {
            var hpStat = (Hp)statsController.GetStat(StatType.Hp);
            if (Mathf.Approximately(hpStat.PeriodicChange, 0f))
            {
                return;
            }

            if (hpStat.PeriodicChange > 0f && !CanRegenerateHp())
            {
                return;
            }

            statsController.AddValue(StatType.Hp, hpStat.PeriodicChange, StatChangeSource.Periodic);
        }

        private void ApplyEmptyAdditionalStatDamage()
        {
            var totalHpDamage = 0f;

            foreach (var statType in AdditionalStatTypes)
            {
                var stat = GetAdditionalPeriodicStat(statType);
                if (stat.Value.Value <= stat.Min)
                {
                    totalHpDamage += Mathf.Abs(stat.PeriodicHpDamageWhenEmpty);
                }
            }

            if (!Mathf.Approximately(totalHpDamage, 0f))
            {
                statsController.AddValue(StatType.Hp, -totalHpDamage, StatChangeSource.Periodic);
            }
        }

        private void ApplyPeriodicChange(StatType statType)
        {
            var periodicStat = GetPeriodicStat(statType);
            if (Mathf.Approximately(periodicStat.PeriodicChange, 0f))
            {
                return;
            }

            statsController.AddValue(statType, periodicStat.PeriodicChange, StatChangeSource.Periodic);
        }

        private PeriodicStat GetPeriodicStat(StatType statType)
        {
            return (PeriodicStat)statsController.GetStat(statType);
        }

        private AdditionalPeriodicStat GetAdditionalPeriodicStat(StatType statType)
        {
            return (AdditionalPeriodicStat)statsController.GetStat(statType);
        }

        private bool CanRegenerateHp()
        {
            var hpStat = statsController.Hp;
            return hpStat.Value.Value > hpStat.Min
                && hpRegenBlockedRemainingTime <= 0f
                && !HasEmptyAdditionalStat();
        }

        private bool HasEmptyAdditionalStat()
        {
            foreach (var statType in AdditionalStatTypes)
            {
                var stat = statsController.GetStat(statType);
                if (stat.Value.Value <= stat.Min)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnStatChanged(StatChangeInfo changeInfo)
        {
            if (changeInfo.StatType != StatType.Hp || changeInfo.CurrentValue >= changeInfo.PreviousValue)
            {
                return;
            }

            hpRegenBlockedRemainingTime = Mathf.Max(
                hpRegenBlockedRemainingTime,
                ((Hp)statsController.GetStat(StatType.Hp)).RegenResumeDelayAfterDamageSeconds);
        }

        public void Dispose()
        {
            hpChangeSubscription.Dispose();
        }
    }
}
