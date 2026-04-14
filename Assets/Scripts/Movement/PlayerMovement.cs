using System.Diagnostics.CodeAnalysis;
using MessagePipe;
using Messages;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerMovement : ITickable
    {
        private const float InputThreshold = 0.001f;

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
            if (!canMove)
            {
                return;
            }

            RotateTowardsCursor();

            if (velocity.sqrMagnitude <= InputThreshold)
            {
                return;
            }

            var moveDirection = Quaternion.Euler(0, cam.transform.rotation.eulerAngles.y, 0) *
                                new Vector3(velocity.x, 0, velocity.y);
            var inputMagnitude = Mathf.Clamp01(velocity.magnitude);
            var moveSpeed = CalculateMoveSpeed(moveDirection) * inputMagnitude;

            controller.Move(moveDirection.normalized * (moveSpeed * Time.deltaTime));
        }

        public void ChangeState(bool newState)
        {
            canMove = newState;
        }

        private void OnMove(PlayerMoveMessage msg)
        {
            velocity = msg.Direction;
        }

        private float CalculateMoveSpeed(Vector3 worldMoveDirection)
        {
            var localMoveDirection = playerTransform.InverseTransformDirection(worldMoveDirection.normalized);
            var horizontalSpeed = Mathf.Abs(localMoveDirection.x) > InputThreshold ? playerMovementConfig.StrafeSpeed : 0f;
            var verticalSpeed = 0f;

            if (localMoveDirection.z > InputThreshold)
            {
                verticalSpeed = playerMovementConfig.ForwardSpeed;
            }
            else if (localMoveDirection.z < -InputThreshold)
            {
                verticalSpeed = playerMovementConfig.BackwardSpeed;
            }

            if (horizontalSpeed > 0f && verticalSpeed > 0f)
            {
                return (horizontalSpeed + verticalSpeed) * 0.5f;
            }

            return Mathf.Max(horizontalSpeed, verticalSpeed);
        }

        private void RotateTowardsCursor()
        {
            var pointer = Pointer.current;

            if (pointer == null)
            {
                return;
            }

            var ray = cam.ScreenPointToRay(pointer.position.ReadValue());
            var groundPlane = new Plane(Vector3.up, playerTransform.position);

            if (!groundPlane.Raycast(ray, out var hitDistance))
            {
                return;
            }

            var lookPoint = ray.GetPoint(hitDistance);
            var lookDirection = lookPoint - playerTransform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude <= InputThreshold)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            playerTransform.rotation = Quaternion.RotateTowards(
                playerTransform.rotation,
                targetRotation,
                playerMovementConfig.RotationSpeed * Time.deltaTime);
        }
    }
}
