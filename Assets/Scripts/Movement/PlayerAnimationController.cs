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
        private const float DirectionThreshold = 0.35f;
        private const string LocomotionStateParameter = "LocomotionState";
        private const int IdleState = 0;
        private const int WalkForwardState = 1;
        private const int WalkForwardLeftState = 2;
        private const int WalkForwardRightState = 3;
        private const int WalkLeftState = 4;
        private const int WalkRightState = 5;
        private const int WalkBackwardState = 6;
        private const int WalkBackwardLeftState = 7;
        private const int WalkBackwardRightState = 8;
        private const int RunForwardState = 9;
        private const int RunForwardLeftState = 10;
        private const int RunForwardRightState = 11;
        private const int RunLeftState = 12;
        private const int RunRightState = 13;
        private const int RunBackwardState = 14;
        private const int RunBackwardLeftState = 15;
        private const int RunBackwardRightState = 16;

        private readonly Camera cam;
        private readonly Animator animator;
        private readonly PlayerMovement playerMovement;
        private readonly Transform visualTransform;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;

        private int currentState = -1;
        private bool isGameplayActive = true;

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
            ChangeState(IdleState);
        }

        public void Tick()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            var movementInput = playerMovement.CurrentVelocity;

            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                ChangeState(IdleState);
                return;
            }

            var moveDirection = Quaternion.Euler(0f, cam.transform.rotation.eulerAngles.y, 0f) *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection.normalized);

            ChangeState(GetStateId(localMoveDirection));
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            isGameplayActive = msg.GameMode is GameMode.Game;

            if (!isGameplayActive)
            {
                ChangeState(IdleState);
            }
        }

        private int GetStateId(Vector3 localMoveDirection)
        {
            var hasHorizontal = Mathf.Abs(localMoveDirection.x) > DirectionThreshold;
            var hasForward = localMoveDirection.z > DirectionThreshold;
            var hasBackward = localMoveDirection.z < -DirectionThreshold;

            if (hasForward)
            {
                if (hasHorizontal)
                {
                    return playerMovement.IsRunning
                        ? (localMoveDirection.x < 0f ? RunForwardLeftState : RunForwardRightState)
                        : (localMoveDirection.x < 0f ? WalkForwardLeftState : WalkForwardRightState);
                }

                return playerMovement.IsRunning ? RunForwardState : WalkForwardState;
            }

            if (hasBackward)
            {
                if (hasHorizontal)
                {
                    return playerMovement.IsRunning
                        ? (localMoveDirection.x < 0f ? RunBackwardLeftState : RunBackwardRightState)
                        : (localMoveDirection.x < 0f ? WalkBackwardLeftState : WalkBackwardRightState);
                }

                return playerMovement.IsRunning ? RunBackwardState : WalkBackwardState;
            }

            if (hasHorizontal)
            {
                return playerMovement.IsRunning
                    ? (localMoveDirection.x < 0f ? RunLeftState : RunRightState)
                    : (localMoveDirection.x < 0f ? WalkLeftState : WalkRightState);
            }

            return IdleState;
        }

        private void ChangeState(int stateId)
        {
            if (currentState == stateId)
            {
                return;
            }

            currentState = stateId;
            animator.SetInteger(LocomotionStateParameter, stateId);
        }
    }
}
