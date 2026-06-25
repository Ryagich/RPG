using UnityEngine;
using UnityEngine.InputSystem;
using TargetLock;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMovement
    {
        private readonly CameraConfig config;
        private readonly TargetLockConfig targetLockConfig;
        private readonly Transform cameraTransform;
        private readonly Transform facingTarget;

        private Transform target;
        private Transform lockTarget;
        private float yaw;
        private float pitch;
        private float targetLockYawOffset;
        private float targetLockPitchOffset;
        private bool hasOrbitSeed;
        private bool isInitialized;
        private bool lookInputEnabled = true;

        public CameraMovement(
            CameraConfig config,
            TargetLockConfig targetLockConfig,
            Transform cameraTransform,
            Transform target,
            Transform facingTarget)
        {
            this.config = config;
            this.targetLockConfig = targetLockConfig;
            this.cameraTransform = cameraTransform;
            this.target = target;
            this.facingTarget = facingTarget;
        }

        public void Tick(float deltaTime)
        {
            if (target == null)
            {
                return;
            }

            EnsureInitialized();
            if (lockTarget != null)
            {
                UpdateTargetLockLook(deltaTime);
            }
            else
            {
                UpdateManualLook();
            }

            var pivotPosition = GetPivotPosition();
            var desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = pivotPosition + desiredRotation * GetCameraOffset();
            var lookPoint = GetLookPoint(pivotPosition);
            var desiredLookRotation = Quaternion.LookRotation((lookPoint - desiredPosition).normalized, Vector3.up);

            var positionLerp = 1f - Mathf.Exp(-config.PositionSharpness * deltaTime);
            var rotationLerp = 1f - Mathf.Exp(-config.RotationSharpness * deltaTime);

            cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPosition, positionLerp);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, desiredLookRotation, rotationLerp);
        }

        public void ChangeTarget(Transform t)
        {
            if (target == t && hasOrbitSeed)
            {
                return;
            }

            target = t;
            isInitialized = false;
        }

        public void SetLookInputEnabled(bool isEnabled)
        {
            lookInputEnabled = isEnabled;
        }

        public void SetLockTarget(Transform t)
        {
            if (lockTarget == t)
            {
                return;
            }

            lockTarget = t;
            targetLockYawOffset = 0f;
            targetLockPitchOffset = 0f;
        }

        public Quaternion GetPlanarRotation()
        {
            return Quaternion.Euler(0f, yaw, 0f);
        }

        private void EnsureInitialized()
        {
            if (isInitialized || target == null)
            {
                return;
            }

            if (!hasOrbitSeed)
            {
                var forwardReference = facingTarget != null ? facingTarget.forward : target.forward;
                forwardReference.y = 0f;
                if (forwardReference.sqrMagnitude <= Mathf.Epsilon)
                {
                    forwardReference = Vector3.forward;
                }

                yaw = Quaternion.LookRotation(forwardReference.normalized, Vector3.up).eulerAngles.y;
                pitch = Mathf.Clamp(config.DefaultPitch, config.MinPitch, config.MaxPitch);
                hasOrbitSeed = true;

                var pivotPosition = GetPivotPosition();
                var desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
                var desiredPosition = pivotPosition + desiredRotation * new Vector3(config.ShoulderOffset, 0f, -config.Distance);
                cameraTransform.position = desiredPosition;
                cameraTransform.rotation = Quaternion.LookRotation((pivotPosition - desiredPosition).normalized, Vector3.up);
            }

            isInitialized = true;
        }

        private Vector3 GetPivotPosition()
        {
            return target.position + config.PivotOffset;
        }

        private Vector3 GetCameraOffset()
        {
            if (lockTarget == null)
            {
                return new Vector3(config.ShoulderOffset, 0f, -config.Distance);
            }

            return new Vector3(
                config.ShoulderOffset * targetLockConfig.CameraShoulderOffsetMultiplier,
                0f,
                -config.Distance * targetLockConfig.CameraDistanceMultiplier);
        }

        private Vector3 GetLookPoint(Vector3 pivotPosition)
        {
            if (lockTarget == null)
            {
                return pivotPosition;
            }

            var lockPoint = lockTarget.position + targetLockConfig.CameraTargetOffset;
            return Vector3.Lerp(pivotPosition, lockPoint, targetLockConfig.CameraFocusBlend);
        }

        private void UpdateManualLook()
        {
            if (!lookInputEnabled)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            yaw += delta.x * config.HorizontalSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * config.VerticalSensitivity, config.MinPitch, config.MaxPitch);
        }

        private void UpdateTargetLockLook(float deltaTime)
        {
            UpdateTargetLockManualOffset();

            var pivotPosition = GetPivotPosition();
            var lockPoint = lockTarget.position + targetLockConfig.CameraTargetOffset;
            var direction = lockPoint - pivotPosition;
            direction.y = 0f;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            var targetYaw = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles.y;
            var yawLerp = 1f - Mathf.Exp(-targetLockConfig.CameraYawSharpness * deltaTime);
            yaw = Mathf.LerpAngle(yaw, targetYaw + targetLockYawOffset, yawLerp);
            pitch = Mathf.Clamp(
                targetLockConfig.CameraPitch + targetLockPitchOffset,
                config.MinPitch,
                config.MaxPitch);
        }

        private void UpdateTargetLockManualOffset()
        {
            if (!lookInputEnabled)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            targetLockYawOffset = Mathf.Clamp(
                targetLockYawOffset + delta.x * config.HorizontalSensitivity,
                -targetLockConfig.CameraMaxManualYawOffset,
                targetLockConfig.CameraMaxManualYawOffset);
            targetLockPitchOffset = Mathf.Clamp(
                targetLockPitchOffset - delta.y * config.VerticalSensitivity,
                -targetLockConfig.CameraMaxManualPitchOffset,
                targetLockConfig.CameraMaxManualPitchOffset);
        }
    }
}
