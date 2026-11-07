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

    /// <summary>
    /// Connects the grounded state owned by <see cref="PlayerGravity"/> to the shared character
    /// Animator. While a moving-landing state is entered, the existing root-motion coordinator
    /// moves the CharacterController, so player and NPC follow the clip's authored displacement.
    /// </summary>
    public sealed class CharacterLandingRootMotionController : IStartable, ITickable, IDisposable
    {
        // Landing states live on the dedicated Air layer. Root motion must follow the
        // layer that evaluates the running and sprinting landing clips, not the locomotion layer.
        private const string AirLayerName = "Air";
        private static readonly int IsFallParameterHash = Animator.StringToHash("IsFall");
        private static readonly int RunningLandingStateHash = Animator.StringToHash("A_Land_Running_Masc");
        private static readonly int SprintingLandingStateHash = Animator.StringToHash("A_Land_Sprinting_Masc 0");

        private readonly PlayerGravity gravity;
        private readonly Animator animator;
        private readonly Combat.CharacterRootMotionController rootMotionController;
        private readonly int airLayerIndex;

        private bool isLandingTransitionExpected;
        private bool isRootMotionActive;
        private bool wasAirborne;
        private bool isLandingInProgress;
        private int landingTransitionExpectedFrame = -1;

        public event Action LandingStarted;
        public event Action LandingFinished;

        public CharacterLandingRootMotionController(
            PlayerGravity gravity,
            Animator animator,
            Combat.CharacterRootMotionController rootMotionController)
        {
            this.gravity = gravity;
            this.animator = animator;
            this.rootMotionController = rootMotionController;
            airLayerIndex = animator != null ? animator.GetLayerIndex(AirLayerName) : -1;
        }

        public void Start()
        {
            if (gravity == null || animator == null)
            {
                return;
            }

            gravity.GroundedStateChanged += OnGroundedStateChanged;
            wasAirborne = !gravity.IsGrounded;
            animator.SetBool(IsFallParameterHash, wasAirborne);
        }

        public void Tick()
        {
            if (animator == null || airLayerIndex < 0)
            {
                return;
            }

            var isRootMotionLandingStateActive = IsRootMotionLandingStateActive();
            if (isRootMotionLandingStateActive)
            {
                isLandingTransitionExpected = false;
            }
            else if (isLandingTransitionExpected && Time.frameCount > landingTransitionExpectedFrame)
            {
                // GroundedStateChanged is raised from PlayerGravity's FixedTick before the Animator
                // has evaluated IsFall=false. Retain the source for that render frame: otherwise
                // applyRootMotion is reset before A_Land_Running_Masc can produce its first delta.
                // On the following frame we can safely release it if the controller chose a
                // different landing branch, such as the idle landing.
                isLandingTransitionExpected = false;
            }

            SetRootMotionActive(isLandingTransitionExpected || isRootMotionLandingStateActive);
            if (isLandingInProgress && !isLandingTransitionExpected && !isRootMotionLandingStateActive)
            {
                CompleteLanding();
            }
        }

        public void Dispose()
        {
            if (gravity != null)
            {
                gravity.GroundedStateChanged -= OnGroundedStateChanged;
            }

            isLandingTransitionExpected = false;
            landingTransitionExpectedFrame = -1;
            SetRootMotionActive(false);
            isLandingInProgress = false;
        }

        private void OnGroundedStateChanged(bool isGrounded)
        {
            if (animator == null)
            {
                return;
            }

            if (!isGrounded)
            {
                if (isLandingInProgress)
                {
                    isLandingTransitionExpected = false;
                    landingTransitionExpectedFrame = -1;
                    SetRootMotionActive(false);
                    CompleteLanding();
                }

                wasAirborne = true;
                animator.SetBool(IsFallParameterHash, true);
                return;
            }

            animator.SetBool(IsFallParameterHash, false);
            if (!wasAirborne)
            {
                return;
            }

            wasAirborne = false;
            // Root motion must be available before the Animator evaluates the landing state;
            // otherwise the first displacement frame of the clip is lost.
            isLandingTransitionExpected = true;
            landingTransitionExpectedFrame = Time.frameCount;
            isLandingInProgress = true;
            LandingStarted?.Invoke();
            SetRootMotionActive(true);
        }

        private bool IsRootMotionLandingStateActive()
        {
            if (airLayerIndex < 0)
            {
                return false;
            }

            if (IsRootMotionLandingState(animator.GetCurrentAnimatorStateInfo(airLayerIndex)))
            {
                return true;
            }

            return animator.IsInTransition(airLayerIndex)
                   && IsRootMotionLandingState(animator.GetNextAnimatorStateInfo(airLayerIndex));
        }

        private static bool IsRootMotionLandingState(AnimatorStateInfo state)
        {
            return state.shortNameHash == RunningLandingStateHash
                   || state.shortNameHash == SprintingLandingStateHash;
        }

        private void SetRootMotionActive(bool isActive)
        {
            if (isRootMotionActive == isActive)
            {
                return;
            }

            isRootMotionActive = isActive;
            rootMotionController?.SetRootMotionActive(this, isActive);
        }

        private void CompleteLanding()
        {
            if (!isLandingInProgress)
            {
                return;
            }

            isLandingInProgress = false;
            LandingFinished?.Invoke();
        }
    }
}
