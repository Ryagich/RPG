using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcScanEnemiesBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Scan Enemies")]
    public sealed class NpcScanEnemiesBehaviour : BaseBehaviour
    {
        public override void Logic(StateMachineContext context)
        {
            context?.GetService<NpcCombatService>()?.ScanForEnemy();
        }
    }
}
