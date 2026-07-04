using Player;
using Inventory;
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
            context?.GetService<EquippedWeaponDropService>()?.DropCurrentWeapon();

            DisableCharacterControllers(
                context?.Owner != null ? context.Owner.transform : null,
                context?.GetService<CharacterController>());

            context?.GetService<PlayerRagdollController>()?.ActivateDeathRagdoll();

            var animator = context?.GetService<Animator>();
            if (animator != null)
            {
                Object.Destroy(animator);
            }
        }

        private static void DisableCharacterControllers(Transform root, CharacterController primaryController)
        {
            if (primaryController != null)
            {
                primaryController.enabled = false;
            }

            if (root == null)
            {
                return;
            }

            var controllers = root.GetComponentsInChildren<CharacterController>(true);
            foreach (var controller in controllers)
            {
                if (controller != null)
                {
                    controller.enabled = false;
                }
            }
        }
    }
}
