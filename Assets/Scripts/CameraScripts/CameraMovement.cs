using UnityEngine;
using Input;
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
        private readonly CameraPoseSolver poseSolver = new();

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
            if (!TryUpdateDesiredPose(deltaTime, out var desiredPose))
            {
                return;
            }

            ApplyPose(desiredPose);
        }

        /// <summary>
        /// Applies a pose already resolved by this gameplay rig. Transitional modes use this
        /// when player camera input explicitly takes ownership of the camera again.
        /// </summary>
        public void ApplyPose(Pose desiredPose)
        {
            // Gameplay orbit is a rigid third-person rig: position is solved around the current
            // player pivot, collision may shorten only its arm, and the resulting camera always
            // aims at the current focus point. Damping either position or aim here breaks the
            // invariant that the player stays framed during a fast orbit.
            cameraTransform.SetPositionAndRotation(desiredPose.position, desiredPose.rotation);
        }

        /// <summary>
        /// Applies queued look input and calculates the current gameplay pose without moving the
        /// camera. Transitional camera modes use it so player rotation remains responsive.
        /// </summary>
        public bool TryUpdateDesiredPose(float deltaTime, out Pose desiredPose)
        {
            var lookInput = ConsumeLookInput();
            if (target == null)
            {
                desiredPose = default;
                return false;
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

            desiredPose = GetDesiredPose();
            return true;
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

        /// <summary>Offsets the gameplay orbit around its vertical axis.</summary>
        public void RotateYaw(float degrees)
        {
            if (Mathf.Abs(degrees) > Mathf.Epsilon)
            {
                yaw = Mathf.Repeat(yaw + degrees, 360f);
            }
        }

        public bool IsSettled(float positionTolerance = 0.03f, float rotationTolerance = 0.5f)
        {
            if (!TryGetDesiredPose(out var desiredPose))
            {
                return false;
            }

            return Vector3.Distance(cameraTransform.position, desiredPose.position) <= positionTolerance
                   && Quaternion.Angle(cameraTransform.rotation, desiredPose.rotation) <= rotationTolerance;
        }

        /// <summary>
        /// Returns the pose the rigid gameplay rig would use this frame without moving the camera.
        /// It is used as the destination of non-gameplay camera transitions.
        /// </summary>
        public bool TryGetDesiredPose(out Pose desiredPose)
        {
            if (!isInitialized || target == null)
            {
                desiredPose = default;
                return false;
            }

            desiredPose = GetDesiredPose();
            return true;
        }

        /// <summary>
        /// Returns the configured default gameplay pose used as the stable destination when
        /// leaving a dialogue. The values are local to the player prefab, so the pose follows
        /// a teleported or otherwise repositioned player without inheriting the previous orbit.
        /// </summary>
        public bool TryGetDefaultPose(out Pose desiredPose)
        {
            if (target == null)
            {
                desiredPose = default;
                return false;
            }

            var pivotPosition = GetPivotPosition();
            var desiredPosition = target.TransformPoint(config.DefaultLocalPosition);
            desiredPosition = poseSolver.ResolveCollision(
                pivotPosition,
                desiredPosition,
                config.CollisionLayers,
                config.CollisionRadius,
                config.CollisionPadding,
                target);
            var desiredRotation = poseSolver.GetLookRotation(
                GetDefaultLookPoint(),
                desiredPosition,
                cameraTransform.rotation);
            desiredPose = new Pose(desiredPosition, desiredRotation);
            return true;
        }

        /// <summary>
        /// Aligns the gameplay orbit with the configured default pose before control returns to
        /// the player. This prevents the first gameplay tick from snapping back to the orbit
        /// that was active before the dialogue.
        /// </summary>
        public void ResetToDefaultOrbit()
        {
            if (target == null)
            {
                return;
            }

            var localEulerAngles = config.DefaultLocalEulerAngles;
            yaw = Mathf.Repeat(target.eulerAngles.y + localEulerAngles.y, 360f);
            pitch = Mathf.Clamp(NormalizeAngle(localEulerAngles.x), config.MinPitch, config.MaxPitch);
            distanceMultiplier = GetDefaultDistanceMultiplier();
            targetLockYawOffset = 0f;
            targetLockPitchOffset = 0f;
            pendingLookInput = Vector2.zero;
            hasOrbitSeed = true;
            isInitialized = true;
        }

        private Pose GetDesiredPose()
        {
            var pivotPosition = GetPivotPosition();
            var desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
            var shoulderOffset = GetShoulderWorldOffset();
            var shoulderPivotPosition = pivotPosition + shoulderOffset;
            var desiredPosition = poseSolver.ResolveCollision(
                shoulderPivotPosition,
                shoulderPivotPosition + desiredRotation * GetCameraOffset(),
                config.CollisionLayers,
                config.CollisionRadius,
                config.CollisionPadding,
                target);
            var desiredLookRotation = poseSolver.GetLookRotation(
                GetLookPoint(pivotPosition, shoulderOffset),
                desiredPosition,
                cameraTransform.rotation);
            return new Pose(desiredPosition, desiredLookRotation);
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
                desiredPosition = poseSolver.ResolveCollision(
                    shoulderPivotPosition,
                    desiredPosition,
                    config.CollisionLayers,
                    config.CollisionRadius,
                    config.CollisionPadding,
                    target);
                cameraTransform.position = desiredPosition;
                cameraTransform.rotation = poseSolver.GetLookRotation(
                    GetLookPoint(pivotPosition, shoulderOffset),
                    desiredPosition,
                    cameraTransform.rotation);
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

        private Vector3 GetDefaultLookPoint()
        {
            var localYaw = config.DefaultLocalEulerAngles.y;
            var distanceMultiplierForDefaultPose = GetDefaultDistanceMultiplier();
            var shoulderOffset = Quaternion.Euler(0f, localYaw, 0f)
                                 * (Vector3.right * config.ShoulderOffset * distanceMultiplierForDefaultPose);
            return target.TransformPoint(config.PivotOffset + shoulderOffset);
        }

        private float GetDefaultDistanceMultiplier()
        {
            var localPositionFromPivot = config.DefaultLocalPosition - config.PivotOffset;
            var localPitch = NormalizeAngle(config.DefaultLocalEulerAngles.x);
            var localYaw = config.DefaultLocalEulerAngles.y;
            var defaultOrbitOffset = Quaternion.Euler(0f, localYaw, 0f) * (Vector3.right * config.ShoulderOffset)
                                     + Quaternion.Euler(localPitch, localYaw, 0f) * (Vector3.back * config.Distance);
            var squaredLength = defaultOrbitOffset.sqrMagnitude;
            if (squaredLength <= Mathf.Epsilon)
            {
                return 1f;
            }

            return Mathf.Clamp(
                Vector3.Dot(localPositionFromPivot, defaultOrbitOffset) / squaredLength,
                config.MinimumDistanceMultiplier,
                config.MaximumDistanceMultiplier);
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
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
