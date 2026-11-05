using Gravity;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerGravity : IFixedTickable
    {
        private const float GroundedStickVelocity = -2f;

        private readonly CharacterController controller;
        private readonly GravityConfig config;

        private float verticalVelocity;
        private bool isJumping;
        private bool? lastGroundedState;

        public event Action JumpStarted;
        public event Action JumpLanded;
        public event Action<bool> GroundedStateChanged;

        public bool IsJumping => isJumping;
        public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded;

        public PlayerGravity
            (
                CharacterController controller,
                GravityConfig config
            )
        {
            this.controller = controller;
            this.config = config;
        }

        public void FixedTick()
        {
            if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            {
                return;
            }

            if (controller.isGrounded && verticalVelocity <= 0f)
            {
                // CharacterController updates grounded state from movement resolution.
                // Keep applying a small downward motion so the player starts falling as soon as support disappears.
                verticalVelocity = GroundedStickVelocity;
            }
            else
            {
                // v = v0 + g * dt
                verticalVelocity -= config.Gravity * Time.fixedDeltaTime;
            }

            // Δs = v * dt
            var displacement = new Vector3(0f, verticalVelocity * Time.fixedDeltaTime, 0f);

            var collisionFlags = controller.Move(displacement);
            UpdateGroundedState();
            if (verticalVelocity > 0f && (collisionFlags & CollisionFlags.Above) != 0)
            {
                // A ceiling terminates the rising phase immediately; otherwise the
                // controller would keep retrying the stale upward velocity.
                verticalVelocity = 0f;
            }

            if (isJumping
                && verticalVelocity < 0f
                && ((collisionFlags & CollisionFlags.Below) != 0 || controller.isGrounded))
            {
                verticalVelocity = GroundedStickVelocity;
                isJumping = false;
                JumpLanded?.Invoke();
            }
        }

        /// <summary>
        /// Starts an upward impulse only while the character controller has support.
        /// Keeping this state transition here preserves PlayerGravity as the sole
        /// owner of vertical velocity.
        /// </summary>
        public bool TryStartJump(float initialVerticalVelocity)
        {
            if (controller == null
                || !controller.enabled
                || !controller.gameObject.activeInHierarchy
                || !controller.isGrounded
                || initialVerticalVelocity <= 0f)
            {
                return false;
            }

            verticalVelocity = initialVerticalVelocity;
            isJumping = true;
            JumpStarted?.Invoke();
            return true;
        }

        private void UpdateGroundedState()
        {
            var isGrounded = controller.isGrounded;
            if (lastGroundedState == isGrounded)
            {
                return;
            }

            lastGroundedState = isGrounded;
            GroundedStateChanged?.Invoke(isGrounded);
        }
    }
}
