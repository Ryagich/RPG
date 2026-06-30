using System;
using GameModes;
using MessagePipe;
using Messages;
using Movement;
using Stats;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Player
{
    public sealed class PlayerDeathController : IStartable, IDisposable
    {
        private readonly Transform playerTransform;
        private readonly StatsController statsController;
        private readonly PlayerDeathState deathState;
        private readonly DeathConfig deathConfig;
        private readonly PlayerRagdollController ragdollController;
        private readonly CharacterController characterController;
        private readonly Animator animator;
        private readonly PlayerMovement playerMovement;
        private readonly PlayerAnimationController playerAnimationController;
        private readonly IPublisher<PlayerDiedMessage> playerDiedPublisher;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly CompositeDisposable disposables = new();

        public PlayerDeathController(
            Transform playerTransform,
            StatsController statsController,
            PlayerDeathState deathState,
            DeathConfig deathConfig,
            PlayerRagdollController ragdollController,
            CharacterController characterController,
            Animator animator,
            PlayerMovement playerMovement,
            PlayerAnimationController playerAnimationController,
            IPublisher<PlayerDiedMessage> playerDiedPublisher,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.playerTransform = playerTransform;
            this.statsController = statsController;
            this.deathState = deathState;
            this.deathConfig = deathConfig;
            this.ragdollController = ragdollController;
            this.characterController = characterController;
            this.animator = animator;
            this.playerMovement = playerMovement;
            this.playerAnimationController = playerAnimationController;
            this.playerDiedPublisher = playerDiedPublisher;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
        }

        public void Start()
        {
            ragdollController?.ConfigureTriggerRagdoll();
            statsController.Hp.Value.Subscribe(OnHpChanged).AddTo(disposables);
            TryDie();
        }

        public void Dispose()
        {
            disposables.Dispose();
        }

        private void OnHpChanged(float _)
        {
            TryDie();
        }

        private void TryDie()
        {
            if (deathState.IsDead || statsController.Hp.Value.Value > statsController.Hp.Min)
            {
                return;
            }

            if (deathConfig != null && deathConfig.CannotDie)
            {
                return;
            }

            deathState.MarkDead();
            playerDiedPublisher.Publish(new PlayerDiedMessage(playerTransform.gameObject, playerTransform));

            playerMovement?.ChangeState(false);
            playerAnimationController?.SetLocomotionLocked(true);

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            ragdollController?.ActivateDeathRagdoll();

            if (animator != null)
            {
                Object.Destroy(animator);
            }

            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Death));
        }
    }
}
