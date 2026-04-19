using Inventory.Inventories;
using Movement;
using UI;
using UnityEngine;
using VContainer.Unity;

namespace Stats
{
    public class StaminaMovementChanger : IStartable, ITickable
    {
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly PlayerInventory playerInventory;
        private readonly PlayerMovement playerMovement;

        private float elapsedTime;
        private bool isStarted;

        public StaminaMovementChanger(
            StatsConfig statsConfig,
            StatsController statsController,
            PlayerInventory playerInventory,
            PlayerMovement playerMovement)
        {
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.playerInventory = playerInventory;
            this.playerMovement = playerMovement;
        }

        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            elapsedTime = 0f;
        }

        public void Tick()
        {
            if (!isStarted)
            {
                return;
            }

            var interval = Mathf.Max(statsConfig.PeriodicChangeIntervalSeconds, float.Epsilon);
            elapsedTime += Time.deltaTime;

            while (elapsedTime >= interval)
            {
                elapsedTime -= interval;
                ApplyMovementDrain();
            }
        }

        private void ApplyMovementDrain()
        {
            if (!playerMovement.IsMoving)
            {
                return;
            }

            var staminaStat = (Stamina)statsController.GetStat(StatType.Stamina);
            var drainMultiplier = staminaStat.EvaluateWeightDrainMultiplier(playerInventory.CurrentWeight, playerInventory.MaxWeight);
            var drainAmount = playerInventory.CurrentWeight * drainMultiplier;
            if (Mathf.Approximately(drainAmount, 0f))
            {
                return;
            }

            if (playerMovement.IsRunning)
            {
                drainAmount *= staminaStat.RunDrainMultiplier;
            }

            statsController.AddValue(StatType.Stamina, -drainAmount, StatChangeSource.Periodic);
        }
    }
}
