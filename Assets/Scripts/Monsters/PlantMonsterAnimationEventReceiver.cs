using Combat;
using UnityEngine;
using VContainer;

namespace Monsters
{
    /// <summary>
    /// Public parameterless contract called by PlantMonster_1 animation clips. It deliberately
    /// contains no state-machine decisions or damage calculation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlantMonsterAnimationEventReceiver : MonoBehaviour
    {
        private PlantMonsterCombatController combat;
        private PlantMonsterNavMeshController navigation;
        private CharacterActionState actionState;

        [Inject]
        public void Construct(
            PlantMonsterCombatController plantMonsterCombat,
            PlantMonsterNavMeshController plantMonsterNavigation,
            CharacterActionState plantMonsterActionState)
        {
            combat = plantMonsterCombat;
            navigation = plantMonsterNavigation;
            actionState = plantMonsterActionState;
        }

        public void BeginDamageWindow() => combat?.BeginDamageWindow();
        public void EndDamageWindow() => combat?.EndDamageWindow();

        // Matches the NPC/player event contract: action state blocks new gameplay actions,
        // while navigation lock prevents NavMeshAgent from moving the monster mid-animation.
        public void LockMovement()
        {
            actionState?.SetActionBlocked(true);
            navigation?.SetMovementLocked(true);
        }

        public void UnlockMovement()
        {
            actionState?.SetActionBlocked(false);
            navigation?.SetMovementLocked(false);
        }

    }
}
