using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcSheatheWeaponBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Sheathe Weapon")]
    public sealed class NpcSheatheWeaponBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(false);
            context?.GetService<NpcCombatService>()?.SheatheWeapon();
        }
    }
}
