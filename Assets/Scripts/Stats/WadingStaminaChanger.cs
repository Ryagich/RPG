using Movement;
using UI;
using UnityEngine;
using VContainer.Unity;

namespace Stats
{
    /// <summary>
    /// Applies the water-configured stamina cost while the player moves in Wading.
    /// It is separate from weight, run, and sprint stamina rules.
    /// </summary>
    public sealed class WadingStaminaChanger : IStartable, ITickable
    {
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly PlayerMovement playerMovement;
        private readonly CharacterWaterDetector waterDetector;
        private readonly CharacterWaterConfig waterConfig;

        private float elapsedTime;
        private bool isStarted;

        public WadingStaminaChanger(
            StatsConfig statsConfig,
            StatsController statsController,
            PlayerMovement playerMovement,
            CharacterWaterDetector waterDetector,
            CharacterWaterConfig waterConfig)
        {
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.playerMovement = playerMovement;
            this.waterDetector = waterDetector;
            this.waterConfig = waterConfig;
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
                ApplyWadingDrain(interval);
            }
        }

        private void ApplyWadingDrain(float interval)
        {
            if (!waterDetector.IsWading || !playerMovement.IsMoving)
            {
                return;
            }

            var drainAmount = waterConfig.WadingStaminaDrainPerSecond * interval;
            if (!Mathf.Approximately(drainAmount, 0f))
            {
                statsController.AddValue(StatType.Stamina, -drainAmount, StatChangeSource.Periodic);
            }
        }
    }
}
