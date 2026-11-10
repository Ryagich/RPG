using Combat;
using UnityEngine;
using VContainer;

namespace Monsters
{
    /// <summary>
    /// Animation-event adapter for PlantMonster_1 clips. It deliberately contains no
    /// state-machine decisions or damage calculation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlantMonsterAnimationEventReceiver : MonoBehaviour
    {
        private PlantMonsterCombatController combat;
        private PlantMonsterNavMeshController navigation;
        private CharacterActionState actionState;
        private string controlLockOwnerAnimationName;

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
        public void LockMovement(string animationName)
        {
            controlLockOwnerAnimationName = animationName;
            actionState?.SetActionBlocked(true);
            navigation?.SetMovementLocked(true);
        }

        public void UnlockMovement(string animationName)
        {
            if (controlLockOwnerAnimationName != animationName)
            {
                return;
            }

            controlLockOwnerAnimationName = null;
            actionState?.SetActionBlocked(false);
            navigation?.SetMovementLocked(false);
        }

    }
}
