using UnityEngine;

namespace Monsters
{
    /// <summary>
    /// Owns the plant monster's attack window. Animation events request its boundaries;
    /// the trigger zone remains a presentation-physics adapter.
    /// </summary>
    public sealed class PlantMonsterCombatController
    {
        private readonly PlantMonsterDamageZone damageZone;
        private readonly float contactDamage;

        public PlantMonsterCombatController(PlantMonsterDamageZone damageZone, PlantMonsterAttackSettings attackSettings)
        {
            this.damageZone = damageZone;
            contactDamage = attackSettings.ContactDamage;
        }

        public bool IsDamageWindowOpen { get; private set; }

        public void BeginDamageWindow()
        {
            damageZone?.BeginDamageWindow(contactDamage);
            IsDamageWindowOpen = damageZone != null && contactDamage > 0f;
        }

        public void EndDamageWindow()
        {
            damageZone?.EndDamageWindow();
            IsDamageWindowOpen = false;
        }
    }
}
