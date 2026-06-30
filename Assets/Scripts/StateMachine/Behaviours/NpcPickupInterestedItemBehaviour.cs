using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcPickupInterestedItemBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Pickup Interested Item")]
    public sealed class NpcPickupInterestedItemBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            if (context == null)
            {
                return;
            }

            context.GetService<NpcItemInterest>()?.SetState("PickingUp");
            if (context.TryGetValue<NpcItemPickupPlan>(NpcItemStateKeys.PickupPlan, out var plan))
            {
                context.GetService<NpcItemPickupService>()?.TryPickup(plan);
            }

            context.SetValue(NpcItemStateKeys.ChainPickupFound, false);
            if (context.TryGetValue<Vector3>(NpcItemStateKeys.HomePosition, out var homePosition)
             && context.TryGetValue<Quaternion>(NpcItemStateKeys.HomeRotation, out var homeRotation)
             && NpcVisibleItemSearch.TryScanAndSetBestTarget(context, homePosition, homeRotation))
            {
                context.SetValue(NpcItemStateKeys.ChainPickupFound, true);
            }

            context.SetValue(NpcItemStateKeys.PickupCompleted, true);
        }
    }
}
