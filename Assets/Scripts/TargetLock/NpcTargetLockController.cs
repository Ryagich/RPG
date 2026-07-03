using UnityEngine;

namespace TargetLock
{
    public sealed class NpcTargetLockController
    {
        private const float DirectionEpsilon = 0.001f;

        private readonly TargetLockConfig config;
        private readonly Transform ownerTransform;

        public NpcTargetLockController(TargetLockConfig config, Animator animator, Transform ownerTransform)
        {
            this.config = config;
            this.ownerTransform = ownerTransform != null ? ownerTransform : animator != null ? animator.transform : null;
        }

        public bool TryFace(TargetLockTarget target)
        {
            if (ownerTransform == null || target == null || !target.IsTargetable)
            {
                return false;
            }

            var direction = target.AimPosition - ownerTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                return false;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            var rotationSpeed = config != null ? config.FacingRotationSpeed : 720f;
            ownerTransform.rotation = Quaternion.RotateTowards(
                ownerTransform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
            return true;
        }
    }
}
