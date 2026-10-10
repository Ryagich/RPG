using NPC;
using TargetLock;
using UnityEngine;

namespace Monsters
{
    /// <summary>
    /// Owns PlantMonster_1's short-range, omnidirectional combat perception channel.
    /// The state graph enables it only outside dormant and wake-up states.
    /// </summary>
    public sealed class PlantMonsterCombatVisionController
    {
        private readonly GameObject visionRoot;
        private readonly NpcVision vision;
        private readonly PlantMonsterCombatVisionSensor sensor;

        public PlantMonsterCombatVisionController(
            GameObject visionRoot,
            NpcVision vision,
            PlantMonsterCombatVisionSensor sensor)
        {
            this.visionRoot = visionRoot;
            this.vision = vision;
            this.sensor = sensor;
        }

        public bool IsEnabled => visionRoot != null && visionRoot.activeInHierarchy;
        public PlantMonsterCombatVisionSensor Sensor => sensor;

        public void SetEnabled(bool enabled)
        {
            if (visionRoot == null)
            {
                return;
            }

            if (!enabled)
            {
                sensor?.ClearCandidates();
            }

            if (visionRoot.activeSelf != enabled)
            {
                visionRoot.SetActive(enabled);
            }
        }

        public bool IsInView(TargetLockTarget target) =>
            IsEnabled && vision != null && vision.IsInView(target);
    }
}
