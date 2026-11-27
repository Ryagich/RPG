using UnityEngine;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class CameraMovementInPlace
    {
        private readonly DialogueCameraConfig config;
        private readonly Transform cameraTransform;
        private readonly CameraPoseSolver poseSolver = new();
        private readonly CameraTransitionArc dialogueTransitionArc;
        private readonly CameraTransitionArc returnTransitionArc;

        private Transform framingAnchor;
        private Transform faceAnchor;
        private Vector3 dialoguePositionVelocity;
        private Vector4 dialogueRotationVelocity;
        private Vector3 returnPositionVelocity;
        private Vector4 returnRotationVelocity;
        private float returnInitialPositionDistance;
        private float returnInitialRotationDifference;
        private float returnProgress;
        private bool isReturningToGameplay;

        public bool IsReturnInputReady => !isReturningToGameplay || returnProgress >= config.ReturnInputUnlockProgress;

        public CameraMovementInPlace
            (
                DialogueCameraConfig config,
                Transform cameraTransform,
                CharacterController playerCharacterController
            )
        {
            this.config = config;
            this.cameraTransform = cameraTransform;
            dialogueTransitionArc = new CameraTransitionArc(playerCharacterController);
            returnTransitionArc = new CameraTransitionArc(playerCharacterController);
        }

        public void Tick(float t)
        {
            if (!framingAnchor)
            {
                return;
            }

            var focusPosition = GetFocusPosition();
            var desiredPosition = poseSolver.ResolveCollision(
                focusPosition,
                GetDialogueCameraPosition(),
                config.CollisionLayers,
                config.CollisionRadius,
                config.CollisionPadding,
                framingAnchor,
                faceAnchor);
            var desiredRotation = poseSolver.GetLookRotation(focusPosition, desiredPosition, cameraTransform.rotation);
            var usesAvoidanceArc = dialogueTransitionArc.TryGetNextPosition(
                cameraTransform.position,
                desiredPosition,
                config.PositionSmoothTime,
                t,
                config.PlayerAvoidanceOffset,
                config.ArcCompletionProgress,
                out var transitionPosition);
            cameraTransform.SetPositionAndRotation(
                usesAvoidanceArc
                    ? transitionPosition
                    : CameraPoseSolver.SmoothPosition(
                        cameraTransform.position,
                        desiredPosition,
                        ref dialoguePositionVelocity,
                        config.PositionSmoothTime,
                        t),
                CameraPoseSolver.SmoothRotation(
                    cameraTransform.rotation,
                    desiredRotation,
                    ref dialogueRotationVelocity,
                    config.RotationSmoothTime,
                    t));
        }

        /// <summary>
        /// The frame follows the actor root to avoid animation jitter. An optional face anchor
        /// keeps the look point on a head bone when one is authored.
        /// </summary>
        public void ChangeTarget(Transform newFramingAnchor, Transform newFaceAnchor)
        {
            framingAnchor = newFramingAnchor;
            faceAnchor = newFaceAnchor;
            dialogueTransitionArc.Reset();
            ResetSmoothing();
        }

        public void ClearTarget()
        {
            framingAnchor = null;
            faceAnchor = null;
            dialogueTransitionArc.Reset();
            returnTransitionArc.Reset();
            ResetSmoothing();
        }

        public void BeginGameplayReturn(Pose targetPose)
        {
            returnTransitionArc.Reset();
            returnPositionVelocity = Vector3.zero;
            returnRotationVelocity = Vector4.zero;
            returnInitialPositionDistance = Vector3.Distance(cameraTransform.position, targetPose.position);
            returnInitialRotationDifference = Quaternion.Angle(cameraTransform.rotation, targetPose.rotation);
            returnProgress = 0f;
            isReturningToGameplay = true;
        }

        /// <summary>
        /// Ends only the automated return. The gameplay rig owns the pose from this point, so
        /// retained return velocity or an active avoidance route must not affect a later session.
        /// </summary>
        public void CancelGameplayReturn()
        {
            returnTransitionArc.Reset();
            returnPositionVelocity = Vector3.zero;
            returnRotationVelocity = Vector4.zero;
            returnInitialPositionDistance = 0f;
            returnInitialRotationDifference = 0f;
            returnProgress = 1f;
            isReturningToGameplay = false;
        }

        public bool TickGameplayReturn(float deltaTime, Pose targetPose)
        {
            var usesAvoidanceArc = returnTransitionArc.TryGetNextPosition(
                cameraTransform.position,
                targetPose.position,
                config.ReturnPositionSmoothTime,
                deltaTime,
                config.PlayerAvoidanceOffset,
                config.ArcCompletionProgress,
                out var transitionPosition);

            cameraTransform.SetPositionAndRotation(
                usesAvoidanceArc
                    ? transitionPosition
                    : CameraPoseSolver.SmoothPosition(
                        cameraTransform.position,
                        targetPose.position,
                        ref returnPositionVelocity,
                        config.ReturnPositionSmoothTime,
                        deltaTime),
                CameraPoseSolver.SmoothRotation(
                    cameraTransform.rotation,
                    targetPose.rotation,
                    ref returnRotationVelocity,
                    config.ReturnRotationSmoothTime,
                    deltaTime));

            UpdateReturnProgress(targetPose);

            if (Vector3.Distance(cameraTransform.position, targetPose.position) > config.ReturnPositionTolerance ||
                Quaternion.Angle(cameraTransform.rotation, targetPose.rotation) > config.ReturnRotationTolerance)
            {
                return false;
            }

            cameraTransform.SetPositionAndRotation(targetPose.position, targetPose.rotation);
            returnProgress = 1f;
            isReturningToGameplay = false;
            return true;
        }

        private void UpdateReturnProgress(Pose targetPose)
        {
            var positionProgress = returnInitialPositionDistance <= Mathf.Epsilon
                ? 1f
                : 1f - Vector3.Distance(cameraTransform.position, targetPose.position) / returnInitialPositionDistance;
            var rotationProgress = returnInitialRotationDifference <= Mathf.Epsilon
                ? 1f
                : 1f - Quaternion.Angle(cameraTransform.rotation, targetPose.rotation) / returnInitialRotationDifference;
            returnProgress = Mathf.Clamp01(Mathf.Min(positionProgress, rotationProgress));
        }

        private Vector3 GetFocusPosition()
        {
            return faceAnchor != null
                ? faceAnchor.position
                : framingAnchor.TransformPoint(config.FocusLocalOffset);
        }

        private Vector3 GetDialogueCameraPosition()
        {
            return framingAnchor.TransformPoint(config.CameraLocalOffset);
        }

        private void ResetSmoothing()
        {
            dialoguePositionVelocity = Vector3.zero;
            dialogueRotationVelocity = Vector4.zero;
            returnPositionVelocity = Vector3.zero;
            returnRotationVelocity = Vector4.zero;
            returnInitialPositionDistance = 0f;
            returnInitialRotationDifference = 0f;
            returnProgress = 0f;
            isReturningToGameplay = false;
        }

    }
}
