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
        private readonly CharacterWaterDetector waterDetector;

        public PlayerFacingController(CameraMotor cameraMotor, Animator animator,
            PlayerMovementConfig config, TargetLockController targetLockController,
            CharacterWaterDetector waterDetector)
        {
            this.cameraMotor = cameraMotor;
            visualTransform = animator.transform;
            this.config = config;
            this.targetLockController = targetLockController;
            this.waterDetector = waterDetector;
        }

        public void Rotate(Vector2 acceptedDirection, Vector2 input, bool isRunning, bool isSprinting, float deltaTime)
        {
            if (targetLockController.IsLocked)
            {
                return;
            }

            if (TryGetSwimSidewaysRotationSpeed(acceptedDirection, isRunning, isSprinting, out var swimRotationSpeed))
            {
                RotateTowardsInput(acceptedDirection, swimRotationSpeed, deltaTime, rotateCameraWithCharacter: true);
                return;
            }

            if (isSprinting && waterDetector?.IsSwimming != true)
            {
                RotateTowardsInput(input, config.SprintRotationSpeed, deltaTime);
                return;
            }

            if (acceptedDirection.y <= Threshold)
            {
                return;
            }

            var worldDirection = cameraMotor.GetGameplayPlanarRotation() *
                                 new Vector3(acceptedDirection.x, 0f, acceptedDirection.y);
            if (worldDirection.sqrMagnitude <= Threshold)
            {
                return;
            }

            var speed = isRunning ? config.RunRotationSpeed : config.WalkRotationSpeed;
            visualTransform.rotation = Quaternion.RotateTowards(visualTransform.rotation,
                Quaternion.LookRotation(worldDirection.normalized, Vector3.up), speed * deltaTime);
        }

        private bool TryGetSwimSidewaysRotationSpeed(
            Vector2 acceptedDirection,
            bool isRunning,
            bool isSprinting,
            out float rotationSpeed)
        {
            rotationSpeed = 0f;
            if (waterDetector?.IsSwimming != true
                || !isRunning
                || Mathf.Abs(acceptedDirection.x) <= Threshold
                || Mathf.Abs(acceptedDirection.y) > Threshold)
            {
                return false;
            }

            rotationSpeed = isSprinting
                ? config.SwimSprintSidewaysRotationSpeed
                : config.SwimRunSidewaysRotationSpeed;
            return true;
        }

        private void RotateTowardsInput(
            Vector2 directionInput,
            float rotationSpeed,
            float deltaTime,
            bool rotateCameraWithCharacter = false)
        {
            var direction = cameraMotor.GetGameplayPlanarRotation() *
                            new Vector3(directionInput.x, 0f, directionInput.y);
            if (direction.sqrMagnitude <= Threshold)
            {
                return;
            }

            var currentRotation = visualTransform.rotation;
            var nextRotation = Quaternion.RotateTowards(
                currentRotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up),
                rotationSpeed * deltaTime);
            visualTransform.rotation = nextRotation;

            if (rotateCameraWithCharacter)
            {
                var yawDelta = Mathf.DeltaAngle(
                    currentRotation.eulerAngles.y,
                    nextRotation.eulerAngles.y);
                cameraMotor.RotateGameplayYaw(yawDelta);
            }
        }

    }
}
