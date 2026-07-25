using Combat;
using MessagePipe;
using Messages;
using UnityEngine;

namespace NPC
{
    public sealed class NpcHitReactionController : CharacterHitReactionControllerBase
    {
        private readonly NpcNavMeshController navMeshController;
        private readonly NpcWeaponInHandController weaponInHandController;

        public NpcHitReactionController(
            HitReactionConfig config,
            CharacterActionState actionState,
            CharacterRootMotionController rootMotionController,
            CharacterDamageReceiver ownerDamageReceiver,
            Transform ownerTransform,
            Animator animator,
            ISubscriber<CharacterDamagedMessage> damagedSubscriber,
            NpcNavMeshController navMeshController,
            NpcWeaponInHandController weaponInHandController)
            : base(config, actionState, rootMotionController, ownerDamageReceiver, ownerTransform, animator, damagedSubscriber)
        {
            this.navMeshController = navMeshController;
            this.weaponInHandController = weaponInHandController;
        }

        protected override void OnReactionStarted()
        {
            weaponInHandController?.InterruptByHitReaction();
            navMeshController?.Stop();
        }

        protected override void OnReactionEnded() { }
    }
}
