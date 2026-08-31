using Inventory.Inventories;
using UI;
using UnityEngine;
using VContainer.Unity;

namespace Stats
{
    public class StaminaMovementChanger : IStartable, ITickable
    {
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly IInventory inventory;
        private readonly ICharacterInventoryCapacity inventoryCapacity;
        private readonly IStaminaMovementState movementState;

        private float elapsedTime;
        private bool isStarted;

        public StaminaMovementChanger(
            StatsConfig statsConfig,
            StatsController statsController,
            IInventory inventory,
            ICharacterInventoryCapacity inventoryCapacity,
            IStaminaMovementState movementState)
        {
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.inventory = inventory;
            this.inventoryCapacity = inventoryCapacity;
            this.movementState = movementState;
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
            if (movementState?.IsMoving != true)
            {
                return;
            }

            var staminaStat = (Stamina)statsController.GetStat(StatType.Stamina);
            var drainMultiplier = staminaStat.EvaluateWeightDrainMultiplier(inventoryCapacity.CurrentWeight, inventory.MaxWeight);
            var drainAmount = inventoryCapacity.CurrentWeight * drainMultiplier;
            if (Mathf.Approximately(drainAmount, 0f))
            {
                return;
            }

            if (movementState.IsRunning)
            {
                drainAmount *= staminaStat.RunDrainMultiplier;
            }

            statsController.AddValue(StatType.Stamina, -drainAmount, StatChangeSource.Periodic);
        }
    }
}
