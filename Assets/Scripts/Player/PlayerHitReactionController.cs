using Combat;
using CharacterAnimation;
using GameModes;
using Inventory;
using MessagePipe;
using Messages;
using Movement;
using UnityEngine;

namespace Player
{
    public sealed class PlayerHitReactionController : CharacterHitReactionControllerBase
    {
        private readonly PlayerMovement playerMovement;
        private readonly PlayerAnimationController playerAnimationController;
        private readonly PlayerWeaponInHandController weaponInHandController;
        private readonly PlayerCharacterAnimationControlHandler animationControl;
        private readonly GameModesController gameModesController;

        public PlayerHitReactionController(
            HitReactionConfig config,
            CharacterActionState actionState,
            CharacterRootMotionController rootMotionController,
            CharacterDamageReceiver ownerDamageReceiver,
            Transform ownerTransform,
            Animator animator,
            ISubscriber<CharacterDamagedMessage> damagedSubscriber,
            PlayerMovement playerMovement,
            PlayerAnimationController playerAnimationController,
            PlayerWeaponInHandController weaponInHandController,
            PlayerCharacterAnimationControlHandler animationControl,
            GameModesController gameModesController)
            : base(config, actionState, rootMotionController, ownerDamageReceiver, ownerTransform, animator, damagedSubscriber)
        {
            this.playerMovement = playerMovement;
            this.playerAnimationController = playerAnimationController;
            this.weaponInHandController = weaponInHandController;
            this.animationControl = animationControl;
            this.gameModesController = gameModesController;
        }

        protected override void OnReactionStarted()
        {
            animationControl?.ReleaseAnimationControl();
            weaponInHandController?.InterruptByHitReaction();
            playerMovement?.ChangeState(false);
            playerAnimationController?.SetLocomotionLocked(true);
        }

        protected override void OnReactionEnded()
        {
            if (gameModesController.GameMode is GameMode.Game or GameMode.Inventory)
            {
                playerMovement?.ChangeState(true);
            }

            playerAnimationController?.SetLocomotionLocked(false);
        }
    }
}
