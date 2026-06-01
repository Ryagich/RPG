using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMovement
    {
        private readonly CameraConfig config;
        private readonly Transform cameraTransform;
        private readonly Transform facingTarget;

        private Transform target;
        private float yaw;
        private float pitch;
        private bool hasOrbitSeed;
        private bool isInitialized;
        private bool lookInputEnabled = true;

        public CameraMovement(
            CameraConfig config,
            Transform cameraTransform,
            Transform target,
            Transform facingTarget)
        {
            this.config = config;
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
            UpdateManualLook();

            var pivotPosition = GetPivotPosition();
            var desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = pivotPosition + desiredRotation * new Vector3(config.ShoulderOffset, 0f, -config.Distance);
            var desiredLookRotation = Quaternion.LookRotation((pivotPosition - desiredPosition).normalized, Vector3.up);

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
    }
}
