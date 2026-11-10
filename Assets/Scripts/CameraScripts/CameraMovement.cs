using UnityEngine;
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
        private float distanceMultiplier = 1f;
        private Vector2 pendingLookInput;
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
            var lookInput = ConsumeLookInput();
            if (target == null)
            {
                return;
            }

            EnsureInitialized();
            if (lockTarget != null)
            {
                UpdateTargetLockLook(deltaTime, lookInput);
            }
            else
            {
                UpdateManualLook(lookInput, deltaTime);
            }

            var pivotPosition = GetPivotPosition();
            var desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
            var shoulderOffset = GetShoulderWorldOffset();
            var shoulderPivotPosition = pivotPosition + shoulderOffset;
            var desiredPosition = ResolveCollision(
                shoulderPivotPosition,
                shoulderPivotPosition + desiredRotation * GetCameraOffset());
            var lookPoint = GetLookPoint(pivotPosition, shoulderOffset);
            var desiredLookRotation = GetLookRotation(lookPoint, desiredPosition);

            // Gameplay orbit is a rigid third-person rig: position is solved around the current
            // player pivot, collision may shorten only its arm, and the resulting camera always
            // aims at the current focus point. Damping either position or aim here breaks the
            // invariant that the player stays framed during a fast orbit.
            cameraTransform.SetPositionAndRotation(desiredPosition, desiredLookRotation);
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
            if (!isEnabled)
            {
                pendingLookInput = Vector2.zero;
            }
        }

        /// <summary>
        /// Queues raw pointer delta supplied by the input layer. It is consumed once by the
        /// camera tick so several input events in one frame are preserved.
        /// </summary>
        public void AddLookInput(Vector2 delta)
        {
            if (!lookInputEnabled || delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            pendingLookInput += delta;
        }

        public void ZoomIn()
        {
            ChangeDistanceMultiplier(-0.1f);
        }

        public void ZoomOut()
        {
            ChangeDistanceMultiplier(0.1f);
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
            var forward = cameraTransform.forward;
            forward.y = 0f;

            return forward.sqrMagnitude <= Mathf.Epsilon
                ? Quaternion.Euler(0f, yaw, 0f)
                : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        public bool IsSettled(float positionTolerance = 0.03f, float rotationTolerance = 0.5f)
        {
            if (!isInitialized || target == null)
            {
                return false;
            }

            var pivotPosition = GetPivotPosition();
            var desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
            var shoulderOffset = GetShoulderWorldOffset();
            var shoulderPivotPosition = pivotPosition + shoulderOffset;
            var desiredPosition = ResolveCollision(
                shoulderPivotPosition,
                shoulderPivotPosition + desiredRotation * GetCameraOffset());
            var desiredLookRotation = GetLookRotation(GetLookPoint(pivotPosition, shoulderOffset), desiredPosition);

            return Vector3.Distance(cameraTransform.position, desiredPosition) <= positionTolerance
                   && Quaternion.Angle(cameraTransform.rotation, desiredLookRotation) <= rotationTolerance;
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
                var shoulderOffset = GetShoulderWorldOffset();
                var shoulderPivotPosition = pivotPosition + shoulderOffset;
                var desiredPosition = shoulderPivotPosition + desiredRotation * GetCameraOffset();
                desiredPosition = ResolveCollision(shoulderPivotPosition, desiredPosition);
                cameraTransform.position = desiredPosition;
                cameraTransform.rotation = GetLookRotation(
                    GetLookPoint(pivotPosition, shoulderOffset),
                    desiredPosition);
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
                return new Vector3(0f, 0f, -config.Distance * distanceMultiplier);
            }

            return new Vector3(0f, 0f,
                -config.Distance * distanceMultiplier * targetLockConfig.CameraDistanceMultiplier);
        }

        private Vector3 GetShoulderWorldOffset()
        {
            var shoulderOffset = config.ShoulderOffset * distanceMultiplier;
            if (lockTarget != null)
            {
                shoulderOffset *= targetLockConfig.CameraShoulderOffsetMultiplier;
            }

            return Quaternion.Euler(0f, yaw, 0f) * (Vector3.right * shoulderOffset);
        }

        private void ChangeDistanceMultiplier(float delta)
        {
            if (!lookInputEnabled)
            {
                return;
            }

            distanceMultiplier = Mathf.Clamp(
                distanceMultiplier + delta,
                config.MinimumDistanceMultiplier,
                config.MaximumDistanceMultiplier);
        }

        private Vector3 GetLookPoint(Vector3 pivotPosition, Vector3 shoulderOffset)
        {
            if (lockTarget == null)
            {
                return pivotPosition + shoulderOffset;
            }

            var lockPoint = lockTarget.position + targetLockConfig.CameraTargetOffset;
            return Vector3.Lerp(pivotPosition, lockPoint, targetLockConfig.CameraFocusBlend) + shoulderOffset;
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

        private void UpdateManualLook(Vector2 lookInput, float deltaTime)
        {
            var rotationDelta = GetManualRotationDelta(lookInput, deltaTime);
            if (rotationDelta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            yaw += rotationDelta.x;
            pitch = Mathf.Clamp(
                pitch - rotationDelta.y,
                config.MinPitch,
                config.MaxPitch);
        }

        private void UpdateTargetLockLook(float deltaTime, Vector2 lookInput)
        {
            UpdateTargetLockManualOffset(lookInput, deltaTime);

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

        private void UpdateTargetLockManualOffset(Vector2 lookInput, float deltaTime)
        {
            var rotationDelta = GetManualRotationDelta(lookInput, deltaTime);
            if (rotationDelta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            targetLockYawOffset = Mathf.Clamp(
                targetLockYawOffset + rotationDelta.x,
                -targetLockConfig.CameraMaxManualYawOffset,
                targetLockConfig.CameraMaxManualYawOffset);
            targetLockPitchOffset = Mathf.Clamp(
                targetLockPitchOffset - rotationDelta.y,
                -targetLockConfig.CameraMaxManualPitchOffset,
                targetLockConfig.CameraMaxManualPitchOffset);
        }

        private Vector2 GetManualRotationDelta(Vector2 lookInput, float deltaTime)
        {
            var pointerDelta = lookInput * MouseSensitivitySettings.Multiplier;
            var horizontalRotation = pointerDelta.x
                                     * CameraSensitivitySettings.GetHorizontal(config.HorizontalSensitivity);
            var verticalRotation = pointerDelta.y
                                   * CameraSensitivitySettings.GetVertical(config.VerticalSensitivity);

            return new Vector2(
                ClampRotationDelta(horizontalRotation, config.MaximumHorizontalRotationSpeed, deltaTime),
                ClampRotationDelta(verticalRotation, config.MaximumVerticalRotationSpeed, deltaTime));
        }

        private static float ClampRotationDelta(float requestedRotation, float maximumSpeed, float deltaTime)
        {
            var maximumRotation = maximumSpeed * Mathf.Max(0f, deltaTime);
            return Mathf.Clamp(requestedRotation, -maximumRotation, maximumRotation);
        }

        private Vector2 ConsumeLookInput()
        {
            var lookInput = pendingLookInput;
            pendingLookInput = Vector2.zero;
            return lookInput;
        }
    }
}
