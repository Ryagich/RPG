using Inventory.Looting;
using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    /// <summary>
    /// Owns the plant monster's terminal death presentation and shutdown. The graph decides
    /// when death starts; this behaviour only stops live gameplay actions and exposes loot.
    /// </summary>
    [CreateAssetMenu(fileName = "PlantMonsterDeathBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Death")]
    public sealed class PlantMonsterDeathBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<PlantMonsterCombatController>()?.EndDamageWindow();
            context?.GetService<PlantMonsterAnimationController>()?.CancelExpectedAction();
            context?.GetService<PlantMonsterHitReactionController>()?.CancelReaction();
            context?.GetService<PlantMonsterNavMeshController>()?.Disable();
            context?.GetService<PlantMonsterAnimationController>()?.PlayDeath();
            context?.GetService<CorpseLootController>()?.ActivateCorpse();
        }
    }
}
