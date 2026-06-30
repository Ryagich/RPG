using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcWaitPickupBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Wait Pickup")]
    public sealed class NpcWaitPickupBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.SetValue(NpcItemStateKeys.PickupWaitTimer, 0f);
            context?.GetService<NpcNavMeshController>()?.Stop();
            context?.GetService<NpcItemInterest>()?.SetState("WaitingPickup");
        }

        public override void Logic(StateMachineContext context)
        {
            if (context == null)
            {
                return;
            }

            context.TryGetValue<float>(NpcItemStateKeys.PickupWaitTimer, out var timer);
            context.SetValue(NpcItemStateKeys.PickupWaitTimer, timer + context.DeltaTime);
        }
    }
}
