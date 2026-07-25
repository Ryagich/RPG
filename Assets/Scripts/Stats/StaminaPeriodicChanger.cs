using UI;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Stats
{
    public class StaminaPeriodicChanger : IStartable, ITickable, System.IDisposable
    {
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly IStaminaMovementState movementState;
        private readonly System.IDisposable staminaChangeSubscription;

        private float elapsedTime;
        private float regenBlockedRemainingTime;
        private bool isStarted;

        public StaminaPeriodicChanger(StatsConfig statsConfig, StatsController statsController, IStaminaMovementState movementState)
        {
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.movementState = movementState;
            staminaChangeSubscription = statsController.Changed.Subscribe(OnStatChanged);
        }

        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            elapsedTime = 0f;
            regenBlockedRemainingTime = 0f;
        }

        public void Tick()
        {
            if (!isStarted)
            {
                return;
            }

            regenBlockedRemainingTime = Mathf.Max(0f, regenBlockedRemainingTime - Time.deltaTime);

            var interval = Mathf.Max(statsConfig.PeriodicChangeIntervalSeconds, float.Epsilon);
            elapsedTime += Time.deltaTime;

            while (elapsedTime >= interval)
            {
                elapsedTime -= interval;
                ApplyPeriodicChange();
            }
        }

        private void ApplyPeriodicChange()
        {
            var staminaStat = (Stamina)statsController.GetStat(StatType.Stamina);
            var periodicChange = staminaStat.PeriodicChange > 0f && movementState?.IsMoving == true
                ? staminaStat.MovingRecoveryPeriodicChange
                : staminaStat.PeriodicChange;

            if (Mathf.Approximately(periodicChange, 0f))
            {
                return;
            }

            if (periodicChange > 0f && regenBlockedRemainingTime > 0f)
            {
                return;
            }

            statsController.AddValue(StatType.Stamina, periodicChange, StatChangeSource.Periodic);
        }

        private void OnStatChanged(StatChangeInfo changeInfo)
        {
            if (changeInfo.StatType != StatType.Stamina)
            {
                return;
            }

            var staminaStat = (Stamina)statsController.GetStat(StatType.Stamina);
            if (changeInfo.PreviousValue > staminaStat.Min && changeInfo.CurrentValue <= staminaStat.Min)
            {
                regenBlockedRemainingTime = Mathf.Max(
                    regenBlockedRemainingTime,
                    staminaStat.RegenResumeDelayAfterEmptySeconds);
            }
        }

        public void Dispose()
        {
            staminaChangeSubscription.Dispose();
        }
    }
}
