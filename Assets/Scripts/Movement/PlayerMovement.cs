using System.Diagnostics.CodeAnalysis;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerMovement : ITickable
    {
        private readonly Camera cam;
        private readonly Transform playerTransform;
        private readonly CharacterController controller;
        private readonly PlayerMovementConfig playerMovementConfig;

        private Vector2 velocity;
        private bool canMove = true;

        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        private PlayerMovement
            (
                PlayerMovementConfig playerMovementConfig,
                Camera cam,
                Transform playerTransform,
                CharacterController controller,
                ISubscriber<PlayerMoveMessage> playerMoveSubscriber
            )
        {
            this.playerMovementConfig = playerMovementConfig;
            this.cam = cam;
            this.playerTransform = playerTransform;
            this.controller = controller;   

            playerMoveSubscriber.Subscribe(OnMove);
        }

        public void Tick()
        {
            if (!canMove || velocity is { x: 0, y: 0 })
            {
                return;
            }

            var moveDirection = Quaternion.Euler(0, cam.transform.rotation.eulerAngles.y, 0) *
                                new Vector3(velocity.x, 0, velocity.y);
            var angle = Mathf.Rad2Deg * Mathf.Atan2(moveDirection.x, moveDirection.z);
            playerTransform.rotation = Quaternion.Euler(0, angle, 0);
            controller.Move(playerTransform.forward * (playerMovementConfig.Speed * Time.deltaTime));
        }

        public void ChangeState(bool newState)
        {
            canMove = newState;
        }

        private void OnMove(PlayerMoveMessage msg)
        {
            velocity = msg.Direction;
        }
    }
}