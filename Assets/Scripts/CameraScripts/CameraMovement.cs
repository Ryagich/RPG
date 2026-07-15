using UnityEngine;
using UnityEngine.InputSystem;
using Input;
using TargetLock;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMovement
    {
        private const int MaxCollisionHits = 16;

        private readonly CameraConfig config;
        private readonly TargetLockConfig targetLockConfig;
        private readonly Transform cameraTransform;
        private readonly Transform facingTarget;
        private readonly RaycastHit[] collisionHits = new RaycastHit[MaxCollisionHits];

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
            var desiredPosition = ResolveCollision(pivotPosition, pivotPosition + desiredRotation * GetCameraOffset());
            var lookPoint = GetLookPoint(pivotPosition);

            var positionLerp = 1f - Mathf.Exp(-config.PositionSharpness * deltaTime);
            var rotationLerp = 1f - Mathf.Exp(-config.RotationSharpness * deltaTime);

            var smoothedPosition = Vector3.Lerp(cameraTransform.position, desiredPosition, positionLerp);
            var finalPosition = ResolveCollision(pivotPosition, smoothedPosition);
            var desiredLookRotation = GetLookRotation(lookPoint, finalPosition);

            cameraTransform.position = finalPosition;
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
                desiredPosition = ResolveCollision(pivotPosition, desiredPosition);
                cameraTransform.position = desiredPosition;
                cameraTransform.rotation = GetLookRotation(pivotPosition, desiredPosition);
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

        private Quaternion GetLookRotation(Vector3 lookPoint, Vector3 cameraPosition)
        {
            var direction = lookPoint - cameraPosition;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return cameraTransform.rotation;
            }

            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private Vector3 ResolveCollision(Vector3 pivotPosition, Vector3 desiredPosition)
        {
            var pivotToCamera = desiredPosition - pivotPosition;
            var desiredDistance = pivotToCamera.magnitude;
            if (desiredDistance <= Mathf.Epsilon)
            {
                return desiredPosition;
            }

            var direction = pivotToCamera / desiredDistance;
            var hitCount = config.CollisionRadius > 0f
                ? Physics.SphereCastNonAlloc(
                    pivotPosition,
                    config.CollisionRadius,
                    direction,
                    collisionHits,
                    desiredDistance,
                    config.CollisionLayers,
                    QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(
                    pivotPosition,
                    direction,
                    collisionHits,
                    desiredDistance,
                    config.CollisionLayers,
                    QueryTriggerInteraction.Ignore);

            var closestDistance = desiredDistance;
            var hasBlockingHit = false;
            for (var i = 0; i < hitCount; i++)
            {
                var hit = collisionHits[i];
                if (IsIgnoredCollision(hit.collider) || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                hasBlockingHit = true;
            }

            if (!hasBlockingHit)
            {
                return desiredPosition;
            }

            var adjustedDistance = Mathf.Max(0f, closestDistance - config.CollisionPadding);
            return pivotPosition + direction * adjustedDistance;
        }

        private bool IsIgnoredCollision(Collider collider)
        {
            if (collider == null || target == null)
            {
                return false;
            }

            return collider.transform.root == target.root;
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

            var delta = mouse.delta.ReadValue() * MouseSensitivitySettings.Multiplier;
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

            var delta = mouse.delta.ReadValue() * MouseSensitivitySettings.Multiplier;
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
