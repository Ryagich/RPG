using Player;
using StateMachine.Graph.Model;
using UnityEngine;
using NPC;
using Object = UnityEngine.Object;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcDeathBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Death")]
    public sealed class NpcDeathBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.Disable();
            context?.GetService<NpcEquippedWeaponDropService>()?.DropCurrentWeapon();

            var characterController = context?.GetService<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            context?.GetService<PlayerRagdollController>()?.ActivateDeathRagdoll();

            var animator = context?.GetService<Animator>();
            if (animator != null)
            {
                Object.Destroy(animator);
            }
        }
    }
}
