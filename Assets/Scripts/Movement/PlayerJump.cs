using System;
using Gravity;
using MessagePipe;
using Messages;
using TargetLock;
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
        private readonly TargetLockController targetLockController;
        private readonly PlayerJumpConfig config;
        private readonly GravityConfig gravityConfig;
        private readonly ISubscriber<JumpInputMessage> jumpInputSubscriber;
        private IDisposable jumpInputSubscription;

        public PlayerJump(
            PlayerGravity playerGravity,
            PlayerMovement playerMovement,
            PlayerAnimationController playerAnimationController,
            TargetLockController targetLockController,
            PlayerJumpConfig config,
            GravityConfig gravityConfig,
            ISubscriber<JumpInputMessage> jumpInputSubscriber)
        {
            this.playerGravity = playerGravity;
            this.playerMovement = playerMovement;
            this.playerAnimationController = playerAnimationController;
            this.targetLockController = targetLockController;
            this.config = config;
            this.gravityConfig = gravityConfig;
            this.jumpInputSubscriber = jumpInputSubscriber;
        }

        public void Start()
        {
            jumpInputSubscription = jumpInputSubscriber.Subscribe(OnJumpInput);
            playerGravity.JumpStarted += OnJumpStarted;
            playerGravity.GroundedStateChanged += OnGroundedStateChanged;
            playerAnimationController?.SetFalling(!playerGravity.IsGrounded);

            if (!playerGravity.IsGrounded)
            {
                EnterAirborneState();
            }
        }

        public void Dispose()
        {
            jumpInputSubscription?.Dispose();
            playerGravity.JumpStarted -= OnJumpStarted;
            playerGravity.GroundedStateChanged -= OnGroundedStateChanged;
            playerMovement?.SetAirborneMomentum(this, false);
            playerMovement?.SetMovementLocked(this, false);
            targetLockController?.SetAutomaticFacingPaused(this, false);
        }

        private void OnJumpInput(JumpInputMessage _)
        {
            if (playerGravity == null || config == null || gravityConfig == null)
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
            EnterAirborneState();
            playerAnimationController?.TriggerJump();
        }

        private void EnterAirborneState()
        {
            // Capture the planar velocity before the control lock clears the
            // input-driven velocity. It remains constant until ground contact.
            playerMovement?.SetAirborneMomentum(this, true);
            playerMovement?.SetMovementLocked(this, true);
            targetLockController?.SetAutomaticFacingPaused(this, true);
        }

        private void ExitAirborneState()
        {
            playerMovement?.SetAirborneMomentum(this, false);
            playerMovement?.SetMovementLocked(this, false);
            targetLockController?.SetAutomaticFacingPaused(this, false);
            playerAnimationController?.ApplyBufferedLocomotion();
        }

        private void OnGroundedStateChanged(bool isGrounded)
        {
            playerAnimationController?.SetFalling(!isGrounded);

            if (isGrounded)
            {
                ExitAirborneState();
                return;
            }

            EnterAirborneState();
        }
    }
}
