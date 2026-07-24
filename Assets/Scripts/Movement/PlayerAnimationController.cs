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
        private bool isEvasionDirectionLocked;
        private Vector2 evasionDirectionalInput;

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

            if (isEvasionDirectionLocked || isLocomotionLocked)
            {
                return;
            }

            var movementInput = playerMovement.CurrentVelocity;

            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                ApplyLocomotionParameters(Vector2.zero, false);
                return;
            }

            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection);
            var directionalInput = new Vector2(
                Mathf.Clamp(localMoveDirection.x, -1f, 1f),
                Mathf.Clamp(localMoveDirection.z, -1f, 1f));

            ApplyLocomotionParameters(directionalInput, playerMovement.IsRunning);
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

            // Keep these as float parameters even though the current tree mostly uses -1/0/1.
            // Future input may become analog (virtual stick / gamepad), and this preserves that path
            // without forcing another Animator parameter migration later.
            animator.SetFloat(DirectionXParameter, currentDirectionalX);
            animator.SetFloat(DirectionYParameter, currentDirectionalY);
            animator.SetBool(IsRunParameter, currentIsRun);
        }

        public void SetLocomotionLocked(bool isLocked)
        {
            isLocomotionLocked = isLocked;

            if (isLocked)
            {
                // Dodge and Roll choose a child of their blend trees from DirectionX/DirectionY.
                // Their animation events lock locomotion at time zero, so retain the direction
                // captured at the request instead of overwriting it with zero.
                ApplyLocomotionParameters(
                    isEvasionDirectionLocked ? evasionDirectionalInput : Vector2.zero,
                    false,
                    force: true);
            }
            else
            {
                ApplyLocomotionParameters(Vector2.zero, false, force: true);
            }
        }

        /// <summary>
        /// Captures the direction of a Dodge or Roll request before its animation locks player movement.
        /// The captured local direction is kept in Animator parameters until the corresponding
        /// UnlockMovement event (or an action cancellation) releases it.
        /// </summary>
        public void CaptureEvasionDirection()
        {
            var movementInput = playerMovement.CurrentInputDirection;
            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection);

            evasionDirectionalInput = new Vector2(
                Mathf.Clamp(localMoveDirection.x, -1f, 1f),
                Mathf.Clamp(localMoveDirection.z, -1f, 1f));
            isEvasionDirectionLocked = true;

            // The action transition can begin before the time-zero LockMovement event fires.
            // Write the captured values now so its blend tree never observes the later zeroes.
            ApplyLocomotionParameters(evasionDirectionalInput, false, force: true);
        }

        /// <summary>
        /// Releases the direction captured for a Dodge or Roll. Normal locomotion updates resume on
        /// the next tick once movement is unlocked.
        /// </summary>
        public void ReleaseEvasionDirection()
        {
            isEvasionDirectionLocked = false;
            evasionDirectionalInput = Vector2.zero;
        }

        private static bool AllowsLocomotion(GameMode mode)
        {
            return mode is GameMode.Game or GameMode.Inventory;
        }
    }
}
