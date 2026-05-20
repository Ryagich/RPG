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

        private readonly Camera cam;
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
            Camera cam,
            Animator animator,
            PlayerMovement playerMovement,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.cam = cam;
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

            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                ApplyLocomotionParameters(Vector2.zero, false);
                return;
            }

            var moveDirection = Quaternion.Euler(0f, cam.transform.rotation.eulerAngles.y, 0f) *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection);
            var directionalInput = new Vector2(
                Mathf.Clamp(localMoveDirection.x, -1f, 1f),
                Mathf.Clamp(localMoveDirection.z, -1f, 1f));

            ApplyLocomotionParameters(directionalInput, playerMovement.IsRunning);
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            isGameplayActive = msg.GameMode is GameMode.Game;

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
                ApplyLocomotionParameters(Vector2.zero, false, force: true);
            }
            else
            {
                ApplyLocomotionParameters(Vector2.zero, false, force: true);
            }
        }
    }
}
