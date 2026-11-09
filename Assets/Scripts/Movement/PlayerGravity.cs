using Gravity;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerGravity : ICharacterVerticalMotionSource
    {
        private const float GroundedStickVelocity = -2f;

        private readonly CharacterController controller;
        private readonly GravityConfig config;

        private float verticalVelocity;
        private bool isJumping;
        private bool? lastGroundedState;
        private bool? lastFallingState;

        public event Action JumpStarted;
        public event Action JumpLanded;
        public event Action<bool> GroundedStateChanged;
        public event Action<bool> FallingStateChanged;

        public bool IsJumping => isJumping;
        public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded;
        public bool IsFalling => !IsGrounded && verticalVelocity < 0f;

        public PlayerGravity
            (
                CharacterController controller,
                GravityConfig config
            )
        {
            this.controller = controller;
            this.config = config;
        }

        public float GetVerticalVelocity(float deltaTime, bool wasGrounded)
        {
            if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            {
                return 0f;
            }

            if (wasGrounded && verticalVelocity <= 0f)
            {
                // CharacterController updates grounded state from movement resolution.
                // Keep applying a small downward motion so the player starts falling as soon as support disappears.
                verticalVelocity = GroundedStickVelocity;
            }
            else
            {
                // v = v0 + g * dt
                verticalVelocity -= config.Gravity * deltaTime;
            }

            return verticalVelocity;
        }

        public void OnMotionApplied(CollisionFlags collisionFlags, bool isGrounded)
        {
            UpdateGroundedState();
            if (verticalVelocity > 0f && (collisionFlags & CollisionFlags.Above) != 0)
            {
                // A ceiling terminates the rising phase immediately; otherwise the
                // controller would keep retrying the stale upward velocity.
                verticalVelocity = 0f;
            }

            if (isJumping
                && verticalVelocity < 0f
                && ((collisionFlags & CollisionFlags.Below) != 0 || isGrounded))
            {
                verticalVelocity = GroundedStickVelocity;
                isJumping = false;
                JumpLanded?.Invoke();
            }

            UpdateFallingState(isGrounded);
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

        private void UpdateFallingState(bool isGrounded)
        {
            var isFalling = !isGrounded && verticalVelocity < 0f;
            if (lastFallingState == isFalling)
            {
                return;
            }

            lastFallingState = isFalling;
            FallingStateChanged?.Invoke(isFalling);
        }
    }

    /// <summary>
    /// Maps the authoritative grounded state to the shared Air-layer parameter.
    /// Animation events exclusively own landing movement locks and root motion.
    /// </summary>
    public sealed class CharacterAirborneAnimationController : IStartable, IDisposable
    {
        private static readonly int IsFallParameterHash = Animator.StringToHash("IsFall");

        private readonly PlayerGravity gravity;
        private readonly Animator animator;

        public CharacterAirborneAnimationController(
            PlayerGravity gravity,
            Animator animator)
        {
            this.gravity = gravity;
            this.animator = animator;
        }

        public void Start()
        {
            if (gravity == null || animator == null)
            {
                return;
            }

            gravity.GroundedStateChanged += OnGroundedStateChanged;
            animator.SetBool(IsFallParameterHash, !gravity.IsGrounded);
        }

        public void Dispose()
        {
            if (gravity != null)
            {
                gravity.GroundedStateChanged -= OnGroundedStateChanged;
            }
        }

        private void OnGroundedStateChanged(bool isGrounded)
        {
            animator?.SetBool(IsFallParameterHash, !isGrounded);
        }
    }
}
