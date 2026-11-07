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
        private readonly CharacterLandingRootMotionController landingRootMotionController;
        private readonly TargetLockController targetLockController;
        private readonly PlayerJumpConfig config;
        private readonly GravityConfig gravityConfig;
        private readonly ISubscriber<JumpInputMessage> jumpInputSubscriber;
        private IDisposable jumpInputSubscription;

        public PlayerJump(
            PlayerGravity playerGravity,
            PlayerMovement playerMovement,
            PlayerAnimationController playerAnimationController,
            CharacterLandingRootMotionController landingRootMotionController,
            TargetLockController targetLockController,
            PlayerJumpConfig config,
            GravityConfig gravityConfig,
            ISubscriber<JumpInputMessage> jumpInputSubscriber)
        {
            this.playerGravity = playerGravity;
            this.playerMovement = playerMovement;
            this.playerAnimationController = playerAnimationController;
            this.landingRootMotionController = landingRootMotionController;
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
            landingRootMotionController.LandingStarted += OnLandingStarted;
            landingRootMotionController.LandingFinished += OnLandingFinished;

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
            landingRootMotionController.LandingStarted -= OnLandingStarted;
            landingRootMotionController.LandingFinished -= OnLandingFinished;
            playerAnimationController?.SetAirborneInputActive(false);
            playerMovement?.SetAirborneMomentum(this, false);
            playerMovement?.SetMovementLocked(this, false);
            playerMovement?.SetMovementLocked(landingRootMotionController, false);
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
            EnterAirborneState(config.HorizontalSpeedMultiplier);
            playerAnimationController?.TriggerJump();
        }

        private void EnterAirborneState(float horizontalSpeedMultiplier = 1f)
        {
            // Capture the planar velocity before the control lock clears the
            // input-driven velocity. A jump-specific multiplier is applied once,
            // then the resulting velocity remains constant until ground contact.
            playerMovement?.SetAirborneMomentum(this, true, horizontalSpeedMultiplier);
            playerMovement?.SetMovementLocked(this, true);
            targetLockController?.SetAutomaticFacingPaused(this, true);
            playerAnimationController?.SetAirborneInputActive(true);
        }

        private void ExitAirborneState()
        {
            playerAnimationController?.SetAirborneInputActive(false);
            playerMovement?.SetAirborneMomentum(this, false);
            playerMovement?.SetMovementLocked(this, false);
            targetLockController?.SetAutomaticFacingPaused(this, false);
            playerAnimationController?.ApplyBufferedLocomotion();
        }

        private void OnGroundedStateChanged(bool isGrounded)
        {
            if (isGrounded)
            {
                ExitAirborneState();
                return;
            }

            EnterAirborneState();
        }

        private void OnLandingStarted()
        {
            playerMovement?.SetMovementLocked(landingRootMotionController, true);
            targetLockController?.SetAutomaticFacingPaused(landingRootMotionController, true);
        }

        private void OnLandingFinished()
        {
            playerMovement?.SetMovementLocked(landingRootMotionController, false);
            targetLockController?.SetAutomaticFacingPaused(landingRootMotionController, false);
            playerAnimationController?.ApplyBufferedLocomotion();
        }
    }
}
