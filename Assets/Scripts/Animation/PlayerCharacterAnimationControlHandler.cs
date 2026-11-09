using System;
using GameModes;
using MessagePipe;
using Messages;
using Movement;
using TargetLock;
using VContainer.Unity;

namespace CharacterAnimation
{
    /// <summary>Executes common animation-control events for the player character.</summary>
    public sealed class PlayerCharacterAnimationControlHandler : ICharacterAnimationControlEventHandler, IStartable, IDisposable
    {
        private readonly CharacterAnimationEventReceiver receiver;
        private readonly PlayerMovement movement;
        private readonly PlayerAnimationController animation;
        private readonly TargetLockController targetLock;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        private IDisposable gameModeSubscription;

        public PlayerCharacterAnimationControlHandler(
            CharacterAnimationEventReceiver receiver,
            PlayerMovement movement,
            PlayerAnimationController animation,
            TargetLockController targetLock,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.receiver = receiver;
            this.movement = movement;
            this.animation = animation;
            this.targetLock = targetLock;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public void Start()
        {
            receiver?.Bind(this);
            gameModeSubscription = gameModeChangedSubscriber?.Subscribe(OnGameModeChanged);
        }

        public void Dispose()
        {
            gameModeSubscription?.Dispose();
            ReleaseAnimationControl();
            receiver?.Unbind(this);
        }

        public void LockCharacterControlFromAnimationEvent()
        {
            movement?.SetMovementLocked(this, true);
            animation?.SetLocomotionLocked(this, true);
            targetLock?.SetAutomaticFacingPaused(this, true);
        }

        public void UnlockCharacterControlFromAnimationEvent()
        {
            movement?.SetMovementLocked(this, false);
            animation?.SetLocomotionLocked(this, false);
            targetLock?.SetAutomaticFacingPaused(this, false);
        }

        public void ReleaseAnimationControl()
        {
            if (receiver != null)
            {
                receiver.ReleaseAnimationControl();
                return;
            }

            UnlockCharacterControlFromAnimationEvent();
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            if (message.GameMode != GameMode.Game)
            {
                ReleaseAnimationControl();
            }
        }
    }
}
