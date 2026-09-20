using Inventory.Inventories;
using UI;
using UnityEngine;
using VContainer.Unity;

namespace Stats
{
    public class StaminaMovementChanger : IStartable, ITickable
    {
        private readonly StatsConfig statsConfig;
        private readonly StaminaConfig staminaConfig;
        private readonly StatsController statsController;
        private readonly IInventory inventory;
        private readonly ICharacterInventoryCapacity inventoryCapacity;
        private readonly IStaminaMovementState movementState;

        private float elapsedTime;
        private bool isStarted;

        public StaminaMovementChanger(
            StatsConfig statsConfig,
            StaminaConfig staminaConfig,
            StatsController statsController,
            IInventory inventory,
            ICharacterInventoryCapacity inventoryCapacity,
            IStaminaMovementState movementState)
        {
            this.statsConfig = statsConfig;
            this.staminaConfig = staminaConfig;
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

            var drainAmount = staminaConfig.EvaluateMovementDrainPerSecond(
                inventoryCapacity.CurrentWeight,
                inventory.MaxWeight) * Mathf.Max(statsConfig.PeriodicChangeIntervalSeconds, float.Epsilon);
            if (Mathf.Approximately(drainAmount, 0f))
            {
                return;
            }

            if (movementState.IsRunning)
            {
                drainAmount *= staminaConfig.RunDrainMultiplier;
            }

            statsController.AddValue(StatType.Stamina, -drainAmount, StatChangeSource.Periodic);
        }
    }
}
