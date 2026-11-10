using System;
using Gravity;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// Translates a jump intent into the configured take-off impulse. Vertical
    /// integration and grounded-state transitions remain in <see cref="PlayerGravity"/>.
    /// </summary>
    public sealed class PlayerJump : IStartable, IDisposable
    {
        private readonly PlayerGravity playerGravity;
        private readonly PlayerMovement playerMovement;
        private readonly PlayerAnimationController playerAnimationController;
        private readonly PlayerJumpConfig config;
        private readonly GravityConfig gravityConfig;
        private readonly ISubscriber<JumpInputMessage> jumpInputSubscriber;
        private IDisposable jumpInputSubscription;

        public PlayerJump(
            PlayerGravity playerGravity,
            PlayerMovement playerMovement,
            PlayerAnimationController playerAnimationController,
            PlayerJumpConfig config,
            GravityConfig gravityConfig,
            ISubscriber<JumpInputMessage> jumpInputSubscriber)
        {
            this.playerGravity = playerGravity;
            this.playerMovement = playerMovement;
            this.playerAnimationController = playerAnimationController;
            this.config = config;
            this.gravityConfig = gravityConfig;
            this.jumpInputSubscriber = jumpInputSubscriber;
        }

        public void Start()
        {
            jumpInputSubscription = jumpInputSubscriber.Subscribe(OnJumpInput);
            playerGravity.JumpStarted += OnJumpStarted;
            playerGravity.AirborneStateChanged += OnAirborneStateChanged;

            if (playerGravity.IsAirborne)
            {
                EnterAirborneState();
            }
        }

        public void Dispose()
        {
            jumpInputSubscription?.Dispose();
            playerGravity.JumpStarted -= OnJumpStarted;
            playerGravity.AirborneStateChanged -= OnAirborneStateChanged;
            playerAnimationController?.SetAirborneInputActive(false);
            playerMovement?.SetAirborneMomentum(this, false);
        }

        private void OnJumpInput(JumpInputMessage _)
        {
            if (playerGravity == null
                || playerMovement == null
                || playerMovement.IsMovementLocked
                || config == null
                || gravityConfig == null)
            {
                return;
            }

            var gravity = gravityConfig.Gravity;
            if (config.Height <= 0f || gravity <= 0f)
            {
                return;
            }

            // v² = 2gh: config stays in designer-friendly height units while
            // PlayerGravity continues to own the actual vertical velocity.
            var initialVerticalVelocity = Mathf.Sqrt(2f * gravity * config.Height);
            playerGravity.TryStartJump(initialVerticalVelocity);
        }

        private void OnJumpStarted()
        {
            EnterAirborneState(config.HorizontalSpeedMultiplier);
            playerAnimationController?.TriggerJump();
        }

        private void EnterAirborneState(float horizontalSpeedMultiplier = 1f)
        {
            // Capture the take-off velocity once; animation events, not jump physics,
            // own control locks and root motion during the Air-layer action.
            playerMovement?.SetAirborneMomentum(this, true, horizontalSpeedMultiplier);
            playerAnimationController?.SetAirborneInputActive(true);
        }

        private void ExitAirborneState()
        {
            playerAnimationController?.SetAirborneInputActive(false);
            playerMovement?.SetAirborneMomentum(this, false);
            playerAnimationController?.ApplyBufferedLocomotion();
        }

        private void OnAirborneStateChanged(bool isAirborne)
        {
            if (!isAirborne)
            {
                ExitAirborneState();
                return;
            }

            if (!playerGravity.IsJumping)
            {
                EnterAirborneState();
            }
        }

    }
}
