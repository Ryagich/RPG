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
        private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");
        private static readonly int IsFallParameterHash = Animator.StringToHash("IsFall");

        private readonly CameraMotor cameraMotor;
        private readonly Animator animator;
        private readonly PlayerMovement playerMovement;
        private readonly Transform visualTransform;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;

        private float currentDirectionalX;
        private float currentDirectionalY;
        private bool currentIsRun;
        private bool currentIsSprint;
        private bool hasSprintParameter;
        private bool isGameplayActive = true;
        private bool isLocomotionLocked;
        private bool isAirborneInputActive;
        private bool clearJumpTriggerAtLateTick;

        private PlayerAnimationController(
            CameraMotor cameraMotor,
            Animator animator,
            PlayerMovement playerMovement,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.cameraMotor = cameraMotor;
            this.animator = animator;
            this.playerMovement = playerMovement;
            visualTransform = animator.transform;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public void Start()
        {
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
            animator.ResetTrigger(JumpTriggerHash);
            hasSprintParameter = HasBoolParameter(IsSprintParameter);
            ApplyLocomotionParameters(Vector2.zero, false, false, force: true);
        }

        public void Tick()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            if (isLocomotionLocked)
            {
                return;
            }

            var movementInput = isAirborneInputActive
                ? playerMovement.CurrentInputDirection
                : playerMovement.CurrentVelocity;
            var isRunning = isAirborneInputActive
                ? playerMovement.IsRunInputPressed
                : playerMovement.IsRunning;
            var isSprinting = isAirborneInputActive
                ? playerMovement.IsSprintInputPressed
                : playerMovement.IsSprinting;

            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                // Keep the requested locomotion mode while momentum crosses zero. Otherwise a
                // forward-to-backward run can briefly trigger the Run -> Walk transition before
                // the next frame restores Run, despite continuous run input.
                ApplyLocomotionParameters(Vector2.zero, isGameplayActive && isRunning, isGameplayActive && isSprinting);
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

            ApplyLocomotionParameters(directionalInput, isRunning, isSprinting);
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            isGameplayActive = AllowsLocomotion(msg.GameMode);

            if (!isGameplayActive)
            {
                ApplyLocomotionParameters(Vector2.zero, false, false, force: true);
            }
        }

        private void ApplyLocomotionParameters(
            Vector2 directionalInput,
            bool isRunning,
            bool isSprinting,
            bool force = false)
        {
            if (!force
             && Mathf.Approximately(currentDirectionalX, directionalInput.x)
             && Mathf.Approximately(currentDirectionalY, directionalInput.y)
             && currentIsRun == isRunning
             && currentIsSprint == isSprinting)
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
        }

        public void SetLocomotionLocked(bool isLocked)
        {
            isLocomotionLocked = isLocked;

            if (isLocked)
            {
                ApplyLocomotionParameters(Vector2.zero, false, false, force: true);
            }
            else
            {
                ApplyLocomotionParameters(Vector2.zero, false, false, force: true);
            }
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
            var movementInput = playerMovement.CurrentInputDirection;
            var isSprintInputPressed = playerMovement.IsSprintInputPressed;
            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                ApplyLocomotionParameters(
                    Vector2.zero,
                    isGameplayActive && playerMovement.IsRunInputPressed,
                    isGameplayActive && isSprintInputPressed,
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
                playerMovement.IsRunInputPressed,
                isSprintInputPressed,
                force: true);
        }

        /// <summary>
        /// Resolves the direction for a Dodge or Roll from the current movement input.
        /// </summary>
        public Vector2 ResolveEvasionDirection()
        {
            var movementInput = playerMovement.CurrentInputDirection;
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
