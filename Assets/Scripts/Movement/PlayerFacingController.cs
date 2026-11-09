using CameraScripts;
using TargetLock;
using UnityEngine;

namespace Movement
{
    /// <summary>Owns visual facing from accepted player locomotion.</summary>
    public sealed class PlayerFacingController
    {
        private const float Threshold = 0.001f;
        private readonly CameraMotor cameraMotor;
        private readonly Transform visualTransform;
        private readonly PlayerMovementConfig config;
        private readonly TargetLockController targetLockController;

        public PlayerFacingController(CameraMotor cameraMotor, Animator animator,
            PlayerMovementConfig config, TargetLockController targetLockController)
        {
            this.cameraMotor = cameraMotor;
            visualTransform = animator.transform;
            this.config = config;
            this.targetLockController = targetLockController;
        }

        public void Rotate(Vector3 worldDirection, Vector2 input, bool isRunning, bool isSprinting, float deltaTime)
        {
            if (targetLockController.IsLocked)
            {
                return;
            }

            if (isSprinting)
            {
                var direction = cameraMotor.GetGameplayPlanarRotation() * new Vector3(input.x, 0f, input.y);
                if (direction.sqrMagnitude <= Threshold)
                {
                    return;
                }

                visualTransform.rotation = Quaternion.RotateTowards(visualTransform.rotation,
                    Quaternion.LookRotation(direction.normalized, Vector3.up), config.SprintRotationSpeed * deltaTime);
                return;
            }

            if (input.y <= Threshold)
            {
                return;
            }

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= Threshold)
            {
                return;
            }

            var speed = isRunning ? config.RunRotationSpeed : config.WalkRotationSpeed;
            visualTransform.rotation = Quaternion.RotateTowards(visualTransform.rotation,
                Quaternion.LookRotation(worldDirection.normalized, Vector3.up), speed * deltaTime);
        }

    }
}
