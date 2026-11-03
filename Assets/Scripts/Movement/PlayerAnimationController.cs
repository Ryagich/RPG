using CameraScripts;
using GameModes;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    public class PlayerAnimationController : IStartable, ITickable
    {
        private const float InputThreshold = 0.001f;
        private const string DirectionXParameter = "DirectionX";
        private const string DirectionYParameter = "DirectionY";
        private const string DirectionActionXParameter = "DirectionActionX";
        private const string DirectionActionYParameter = "DirectionActionY";
        private const string IsRunParameter = "IsRun";

        private readonly CameraMotor cameraMotor;
        private readonly Animator animator;
        private readonly PlayerMovement playerMovement;
        private readonly Transform visualTransform;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;

        private float currentDirectionalX;
        private float currentDirectionalY;
        private bool currentIsRun;
        private bool isGameplayActive = true;
        private bool isLocomotionLocked;

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
            ApplyLocomotionParameters(Vector2.zero, false, force: true);
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

            var movementInput = playerMovement.CurrentVelocity;
            var isRunning = playerMovement.IsRunning;

            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                // Keep the requested locomotion mode while momentum crosses zero. Otherwise a
                // forward-to-backward run can briefly trigger the Run -> Walk transition before
                // the next frame restores Run, despite continuous run input.
                ApplyLocomotionParameters(Vector2.zero, isGameplayActive && isRunning);
                return;
            }

            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection);
            var directionalInput = new Vector2(
                Mathf.Clamp(localMoveDirection.x, -1f, 1f),
                Mathf.Clamp(localMoveDirection.z, -1f, 1f));

            ApplyLocomotionParameters(directionalInput, isRunning);
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            isGameplayActive = AllowsLocomotion(msg.GameMode);

            if (!isGameplayActive)
            {
                ApplyLocomotionParameters(Vector2.zero, false, force: true);
            }
        }

        private void ApplyLocomotionParameters(Vector2 directionalInput, bool isRunning, bool force = false)
        {
            if (!force
             && Mathf.Approximately(currentDirectionalX, directionalInput.x)
             && Mathf.Approximately(currentDirectionalY, directionalInput.y)
             && currentIsRun == isRunning)
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

            // CurrentVelocity carries both the physical direction and its normalized magnitude.
            // The blend tree uses the magnitude to reflect acceleration and braking, so it must
            // not be normalized here.
            animator.SetFloat(DirectionXParameter, currentDirectionalX);
            animator.SetFloat(DirectionYParameter, currentDirectionalY);
            animator.SetBool(IsRunParameter, currentIsRun);
        }

        public void SetLocomotionLocked(bool isLocked)
        {
            isLocomotionLocked = isLocked;

            if (isLocked)
            {
                ApplyLocomotionParameters(Vector2.zero, false, force: true);
            }
            else
            {
                ApplyLocomotionParameters(Vector2.zero, false, force: true);
            }
        }

        /// <summary>
        /// Writes the DirectionAction parameters used exclusively by Dodge and Roll before the
        /// action request enters the Animator.
        /// </summary>
        public void CaptureEvasionDirection()
        {
            var movementInput = playerMovement.CurrentInputDirection;
            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection);

            var actionDirection = new Vector2(
                Mathf.Clamp(localMoveDirection.x, -1f, 1f),
                Mathf.Clamp(localMoveDirection.z, -1f, 1f));
            animator.SetFloat(DirectionActionXParameter, actionDirection.x);
            animator.SetFloat(DirectionActionYParameter, actionDirection.y);
        }

        private static bool AllowsLocomotion(GameMode mode)
        {
            return mode is GameMode.Game or GameMode.Inventory;
        }
    }
}
