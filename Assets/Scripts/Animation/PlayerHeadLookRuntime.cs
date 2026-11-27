using UnityEngine;
using VContainer;

namespace CharacterAnimation
{
    /// <summary>
    /// Presentation-only head IK for the player. It follows the active camera direction with
    /// independent yaw and pitch speeds, while gameplay state decides when the pose is allowed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHeadLookRuntime : MonoBehaviour
    {
        private const float WeightEpsilon = 0.001f;

        private Animator animator;
        private Camera gameplayCamera;
        private PlayerHeadLookConfig config;
        private ICharacterAnimationState animationState;
        private float currentYaw;
        private float currentPitch;
        private float currentWeight;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        [Inject]
        public void Construct(
            Camera valueCamera,
            PlayerHeadLookConfig valueConfig,
            ICharacterAnimationState valueAnimationState)
        {
            gameplayCamera = valueCamera;
            config = valueConfig;
            animationState = valueAnimationState;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman || !animator.isActiveAndEnabled || config == null)
            {
                return;
            }

            var canLook = CanApplyHeadLook();
            var targetWeight = canLook ? config.HeadWeight : 0f;
            var weightSpeed = targetWeight > currentWeight ? config.EnterWeightSpeed : config.ExitWeightSpeed;
            currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, weightSpeed * Time.deltaTime);
            if (currentWeight <= WeightEpsilon)
            {
                currentWeight = 0f;
                animator.SetLookAtWeight(0f);
                return;
            }

            UpdateLookDirection();
            var worldDirection = animator.transform.TransformDirection(BuildLocalDirection(currentYaw, currentPitch));
            animator.SetLookAtPosition(animator.transform.position + worldDirection * config.LookDistance);
            animator.SetLookAtWeight(currentWeight, 0f, 1f, 0f, config.ClampWeight);
        }

        private bool CanApplyHeadLook()
        {
            return gameplayCamera != null
                   && animationState != null
                   && !animationState.IsSprinting
                   && !animationState.IsSwimmingRun
                   && !animationState.IsSwimmingSprint
                   && !animationState.IsAttacking;
        }

        private void UpdateLookDirection()
        {
            var localCameraDirection = animator.transform.InverseTransformDirection(gameplayCamera.transform.forward);
            if (localCameraDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            localCameraDirection.Normalize();
            var desiredYaw = Mathf.Clamp(
                Mathf.Atan2(localCameraDirection.x, localCameraDirection.z) * Mathf.Rad2Deg,
                -config.MaximumYawAngle,
                config.MaximumYawAngle);
            var desiredPitch = Mathf.Clamp(
                Mathf.Asin(localCameraDirection.y) * Mathf.Rad2Deg,
                -config.MaximumDownPitchAngle,
                config.MaximumUpPitchAngle);

            currentYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, config.YawFollowSpeed * Time.deltaTime);
            currentPitch = Mathf.MoveTowards(currentPitch, desiredPitch, config.PitchFollowSpeed * Time.deltaTime);
        }

        private static Vector3 BuildLocalDirection(float yaw, float pitch)
        {
            var yawRadians = yaw * Mathf.Deg2Rad;
            var pitchRadians = pitch * Mathf.Deg2Rad;
            var cosinePitch = Mathf.Cos(pitchRadians);
            return new Vector3(
                Mathf.Sin(yawRadians) * cosinePitch,
                Mathf.Sin(pitchRadians),
                Mathf.Cos(yawRadians) * cosinePitch);
        }
    }
}
