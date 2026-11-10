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
        private const int GroundProbeHitCapacity = 8;

        private readonly CharacterController controller;
        private readonly GravityConfig config;
        private readonly RaycastHit[] groundProbeHits = new RaycastHit[GroundProbeHitCapacity];

        private float verticalVelocity;
        private float airborneGraceElapsed;
        private bool isJumping;
        private bool isAirborne;
        private bool isAirborneGracePending;
        private bool? lastGroundedState;
        private bool? lastFallingState;

        public event Action JumpStarted;
        public event Action JumpLanded;
        public event Action<bool> GroundedStateChanged;
        public event Action<bool> AirborneStateChanged;
        public event Action<bool> FallingStateChanged;

        public bool IsJumping => isJumping;
        public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded;
        public bool IsAirborne => isAirborne;
        public bool IsFalling => isAirborne && verticalVelocity < 0f;

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

            if (isAirborneGracePending && !isJumping)
            {
                // Keep pressing onto the nearby surface while its contact is briefly lost on
                // uneven terrain. Starting gravity here would turn a 0.1-second gap into a fall.
                airborneGraceElapsed += deltaTime;
                verticalVelocity = GroundedStickVelocity;
            }
            else if (wasGrounded && verticalVelocity <= 0f)
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

            UpdateAirborneState(isGrounded);
            UpdateFallingState();
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
            CancelAirborneGrace();
            SetAirborne(true);
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

        private void UpdateAirborneState(bool isGrounded)
        {
            if (isGrounded)
            {
                CancelAirborneGrace();
                SetAirborne(false);
                return;
            }

            if (isJumping)
            {
                CancelAirborneGrace();
                SetAirborne(true);
                return;
            }

            if (isAirborne)
            {
                return;
            }

            var hasNearbyGround = HasGroundWithinFallStartClearance();
            if (!hasNearbyGround)
            {
                CancelAirborneGrace();
                SetAirborne(true);
                return;
            }

            if (config.FallStartGraceDuration <= 0f)
            {
                SetAirborne(true);
                return;
            }

            if (!isAirborneGracePending)
            {
                isAirborneGracePending = true;
                airborneGraceElapsed = 0f;
                return;
            }

            if (airborneGraceElapsed >= config.FallStartGraceDuration)
            {
                CancelAirborneGrace();
                SetAirborne(true);
            }
        }

        private void UpdateFallingState()
        {
            var isFalling = IsFalling;
            if (lastFallingState == isFalling)
            {
                return;
            }

            lastFallingState = isFalling;
            FallingStateChanged?.Invoke(isFalling);
        }

        private bool HasGroundWithinFallStartClearance()
        {
            if (controller == null || config == null)
            {
                return false;
            }

            var radius = Mathf.Max(0.01f, controller.radius - controller.skinWidth);
            var capsuleBottomOffset = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
            var origin = controller.transform.TransformPoint(controller.center)
                         - controller.transform.up * capsuleBottomOffset
                         + controller.transform.up * controller.skinWidth;
            var castDistance = config.FallStartGroundClearance + controller.skinWidth;
            var hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                -controller.transform.up,
                groundProbeHits,
                castDistance,
                config.GroundProbeMask,
                QueryTriggerInteraction.Ignore);

            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = groundProbeHits[index].collider;
                if (hitCollider != null && !BelongsToCharacter(hitCollider.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private bool BelongsToCharacter(Transform candidate)
        {
            if (candidate == null || controller == null)
            {
                return false;
            }

            var characterTransform = controller.transform;
            return candidate == characterTransform
                   || candidate.IsChildOf(characterTransform)
                   || characterTransform.IsChildOf(candidate);
        }

        private void CancelAirborneGrace()
        {
            isAirborneGracePending = false;
            airborneGraceElapsed = 0f;
        }

        private void SetAirborne(bool value)
        {
            if (isAirborne == value)
            {
                return;
            }

            isAirborne = value;
            AirborneStateChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// Maps the filtered airborne state to the shared Air-layer parameter.
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

            gravity.AirborneStateChanged += OnAirborneStateChanged;
            animator.SetBool(IsFallParameterHash, gravity.IsAirborne);
        }

        public void Dispose()
        {
            if (gravity != null)
            {
                gravity.AirborneStateChanged -= OnAirborneStateChanged;
            }
        }

        private void OnAirborneStateChanged(bool isAirborne)
        {
            animator?.SetBool(IsFallParameterHash, isAirborne);
        }
    }
}
