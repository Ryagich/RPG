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
        private const float TransitionDuration = 0.1f;
        private const string BaseLayerName = "Base Layer";
        private const string IdleState = "Base Simple Male Idle";
        private const string WalkForwardState = "WalkForward";
        private const string WalkForwardLeftState = "WalkForwardLeft";
        private const string WalkForwardRightState = "WalkForwardRight";
        private const string WalkLeftState = "WalkLeft";
        private const string WalkRightState = "WalkRight";
        private const string WalkBackwardState = "WalkBackward";
        private const string WalkBackwardLeftState = "WalkBackwardLeft";
        private const string WalkBackwardRightState = "WalkBackwardRight";

        private readonly Camera cam;
        private readonly Animator animator;
        private readonly Transform visualTransform;
        private readonly ISubscriber<PlayerMoveMessage> playerMoveSubscriber;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;

        private Vector2 movementInput;
        private string currentState = string.Empty;
        private bool isGameplayActive = true;

        private PlayerAnimationController(
            Camera cam,
            Animator animator,
            ISubscriber<PlayerMoveMessage> playerMoveSubscriber,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.cam = cam;
            this.animator = animator;
            visualTransform = animator.transform;
            this.playerMoveSubscriber = playerMoveSubscriber;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public void Start()
        {
            playerMoveSubscriber.Subscribe(OnMove);
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
            ChangeState(IdleState);
        }

        public void Tick()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            if (!isGameplayActive || movementInput.sqrMagnitude <= InputThreshold)
            {
                ChangeState(IdleState);
                return;
            }

            var moveDirection = Quaternion.Euler(0f, cam.transform.rotation.eulerAngles.y, 0f) *
                                new Vector3(movementInput.x, 0f, movementInput.y);
            var localMoveDirection = visualTransform.InverseTransformDirection(moveDirection.normalized);

            ChangeState(GetStateName(localMoveDirection));
        }

        private void OnMove(PlayerMoveMessage msg)
        {
            movementInput = msg.Direction;
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            isGameplayActive = msg.GameMode is GameMode.Game;

            if (!isGameplayActive)
            {
                movementInput = Vector2.zero;
                ChangeState(IdleState);
            }
        }

        private string GetStateName(Vector3 localMoveDirection)
        {
            var hasHorizontal = Mathf.Abs(localMoveDirection.x) > DirectionThreshold;
            var hasForward = localMoveDirection.z > DirectionThreshold;
            var hasBackward = localMoveDirection.z < -DirectionThreshold;

            if (hasForward)
            {
                if (hasHorizontal)
                {
                    return localMoveDirection.x < 0f ? WalkForwardLeftState : WalkForwardRightState;
                }

                return WalkForwardState;
            }

            if (hasBackward)
            {
                if (hasHorizontal)
                {
                    return localMoveDirection.x < 0f ? WalkBackwardLeftState : WalkBackwardRightState;
                }

                return WalkBackwardState;
            }

            if (hasHorizontal)
            {
                return localMoveDirection.x < 0f ? WalkLeftState : WalkRightState;
            }

            return IdleState;
        }

        private void ChangeState(string stateName)
        {
            if (currentState == stateName)
            {
                return;
            }

            currentState = stateName;
            animator.CrossFadeInFixedTime($"{BaseLayerName}.{stateName}", TransitionDuration);
        }
    }
}
