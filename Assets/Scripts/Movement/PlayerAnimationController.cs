using System.Collections.Generic;
using CameraScripts;
using Combat;
using GameModes;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    public class PlayerAnimationController : IStartable, ITickable, ILateTickable
    {
        private const float InputThreshold = 0.001f;
        private const string DirectionXParameter = "DirectionX";
        private const string DirectionYParameter = "DirectionY";
        private const string DirectionActionXParameter = "DirectionActionX";
        private const string DirectionActionYParameter = "DirectionActionY";
        private const string IsRunParameter = "IsRun";
        private const string IsSprintParameter = "IsSprint";
        private const string IsIdleParameter = "IsIdle";
        private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");
        private static readonly int IsFallParameterHash = Animator.StringToHash("IsFall");

        private readonly CameraMotor cameraMotor;
        private readonly Animator animator;
        private readonly PlayerMovement playerMovement;
        private readonly PlayerMovementIntent movementIntent;
        private readonly Transform visualTransform;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;

        private float currentDirectionalX;
        private float currentDirectionalY;
        private bool currentIsRun;
        private bool currentIsSprint;
        private bool currentIsIdle;
        private bool hasSprintParameter;
        private bool isGameplayActive = true;
        private bool isLocomotionLocked;
        private readonly HashSet<object> locomotionLockSources = new();
        private bool isAirborneInputActive;
        private bool clearJumpTriggerAtLateTick;

        private PlayerAnimationController(
            CameraMotor cameraMotor,
            Animator animator,
            PlayerMovement playerMovement,
            PlayerMovementIntent movementIntent,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.cameraMotor = cameraMotor;
            this.animator = animator;
            this.playerMovement = playerMovement;
            this.movementIntent = movementIntent;
            visualTransform = animator.transform;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public void Start()
        {
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
            animator.ResetTrigger(JumpTriggerHash);
            hasSprintParameter = HasBoolParameter(IsSprintParameter);
            ApplyLocomotionParameters(Vector2.zero, false, false, true, force: true);
        }

        public void Tick()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            if (IsLocomotionLocked)
            {
                return;
            }

            var movementInput = isAirborneInputActive
                ? movementIntent.Direction
                : playerMovement.CurrentVelocity;
            var isRunning = isAirborneInputActive
                ? movementIntent.IsRunPressed
                : playerMovement.IsRunning;
            var isSprinting = isAirborneInputActive
                ? movementIntent.IsSprintPressed
                : playerMovement.IsSprinting;
            var isIdle = !playerMovement.IsMoving;

            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                // Keep the requested locomotion mode while momentum crosses zero. Otherwise a
                // forward-to-backward run can briefly trigger the Run -> Walk transition before
                // the next frame restores Run, despite continuous run input.
                ApplyLocomotionParameters(
                    Vector2.zero,
                    isGameplayActive && isRunning,
                    isGameplayActive && isSprinting,
                    isIdle);
                return;
            }

            var directionalInput = Vector2.up;
            if (!isSprinting)
            {
                var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                    new Vector3(movementInput.x, 0f, movementInput.y);
                var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection);
                directionalInput = new Vector2(
                    Mathf.Clamp(localMoveDirection.x, -1f, 1f),
                    Mathf.Clamp(localMoveDirection.z, -1f, 1f));
            }

            ApplyLocomotionParameters(directionalInput, isRunning, isSprinting, isIdle);
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            isGameplayActive = AllowsLocomotion(msg.GameMode);

            if (!isGameplayActive)
            {
                ApplyLocomotionParameters(Vector2.zero, false, false, true, force: true);
            }
        }

        private void ApplyLocomotionParameters(
            Vector2 directionalInput,
            bool isRunning,
            bool isSprinting,
            bool isIdle,
            bool force = false)
        {
            if (!force
             && Mathf.Approximately(currentDirectionalX, directionalInput.x)
             && Mathf.Approximately(currentDirectionalY, directionalInput.y)
             && currentIsRun == isRunning
             && currentIsSprint == isSprinting
             && currentIsIdle == isIdle)
            {
                return;
            }

            if (animator == null)
            {
                return;
            }

            currentDirectionalX = directionalInput.x;
            currentDirectionalY = directionalInput.y;
            currentIsRun = isRunning;
            currentIsSprint = isSprinting;
            currentIsIdle = isIdle;

            // CurrentVelocity carries both the physical direction and its normalized magnitude.
            // The blend tree uses the magnitude to reflect acceleration and braking, so it must
            // not be normalized here.
            animator.SetFloat(DirectionXParameter, currentDirectionalX);
            animator.SetFloat(DirectionYParameter, currentDirectionalY);
            animator.SetBool(IsRunParameter, currentIsRun);
            if (hasSprintParameter)
            {
                animator.SetBool(IsSprintParameter, currentIsSprint);
            }

            animator.SetBool(IsIdleParameter, currentIsIdle);
        }

        public void SetLocomotionLocked(bool isLocked)
        {
            isLocomotionLocked = isLocked;

            ApplyLocomotionLockState();
        }

        public void SetLocomotionLocked(object source, bool isLocked)
        {
            if (source == null)
            {
                return;
            }

            if (isLocked)
            {
                locomotionLockSources.Add(source);
            }
            else
            {
                locomotionLockSources.Remove(source);
            }

            ApplyLocomotionLockState();
        }

        private bool IsLocomotionLocked => isLocomotionLocked || locomotionLockSources.Count > 0;

        private void ApplyLocomotionLockState()
        {
            if (IsLocomotionLocked)
            {
                return;
            }

            // A control lock must stop parameter updates, not erase the held intent.
            // Landing and Full Body events can therefore release directly into the input that
            // was held throughout the animation instead of producing a transient idle state.
            ApplyBufferedLocomotion();
        }

        /// <summary>
        /// While airborne, locomotion parameters describe the player's buffered intent
        /// rather than the deliberately frozen physical velocity.
        /// </summary>
        public void SetAirborneInputActive(bool isActive)
        {
            isAirborneInputActive = isActive;
        }

        public void LateTick()
        {
            if (!clearJumpTriggerAtLateTick)
            {
                return;
            }

            clearJumpTriggerAtLateTick = false;
            if (animator != null)
            {
                // A trigger otherwise remains pending when its transition cannot run,
                // which would make a past jump request fire in a later state.
                animator.ResetTrigger(JumpTriggerHash);
            }
        }

        public void TriggerJump()
        {
            if (animator != null)
            {
                animator.ResetTrigger(JumpTriggerHash);
                animator.SetTrigger(JumpTriggerHash);
                clearJumpTriggerAtLateTick = true;
            }
        }

        /// <summary>
        /// Applies the input buffered while movement was locked, without waiting for velocity
        /// acceleration after landing.
        /// </summary>
        public void ApplyBufferedLocomotion()
        {
            var movementInput = movementIntent.Direction;
            var isSprintInputPressed = movementIntent.IsSprintPressed;
            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                ApplyLocomotionParameters(
                    Vector2.zero,
                    isGameplayActive && movementIntent.IsRunPressed,
                    isGameplayActive && isSprintInputPressed,
                    !playerMovement.IsMoving,
                    force: true);
                return;
            }

            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection);
            var directionalInput = new Vector2(
                Mathf.Clamp(localMoveDirection.x, -1f, 1f),
                Mathf.Clamp(localMoveDirection.z, -1f, 1f));
            if (isSprintInputPressed)
            {
                directionalInput = Vector2.up;
            }

            ApplyLocomotionParameters(
                directionalInput,
                movementIntent.IsRunPressed,
                isSprintInputPressed,
                !playerMovement.IsMoving,
                force: true);
        }

        /// <summary>
        /// Resolves the direction for a Dodge or Roll from the current movement input.
        /// </summary>
        public Vector2 ResolveEvasionDirection()
        {
            var movementInput = movementIntent.Direction;
            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection);

            return EvasionAnimationDirectionUtility.SnapToCardinal(localMoveDirection);
        }

        /// <summary>
        /// Writes the DirectionAction parameters used exclusively by Dodge and Roll.
        /// </summary>
        public void SetEvasionDirection(Vector2 actionDirection)
        {
            animator.SetFloat(DirectionActionXParameter, actionDirection.x);
            animator.SetFloat(DirectionActionYParameter, actionDirection.y);
        }

        private static bool AllowsLocomotion(GameMode mode)
        {
            return mode is GameMode.Game or GameMode.Inventory;
        }

        private bool HasBoolParameter(string parameterName)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
