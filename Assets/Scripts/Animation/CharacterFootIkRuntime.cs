using Player;
using UnityEngine;
using VContainer;

namespace CharacterAnimation
{
    /// <summary>
    /// Presentation-only Humanoid IK correction shared by player and NPCs. It samples heel,
    /// centre and toe support points, then blends a bounded correction over the authored pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterFootIkRuntime : MonoBehaviour
    {
        private const int HitBufferSize = 8;

        private sealed class FootState
        {
            public bool HasHeightBaseline;
            public float HeightBaseline;
            public float ContactWeight;
            public float IkWeight;
            public bool HasSmoothedPose;
            public Vector3 SmoothedPosition;
            public Quaternion SmoothedRotation;
        }

        private struct GroundProbe
        {
            public bool IsValid;
            public RaycastHit Hit;
            public float Quality;
        }

        private struct GroundPose
        {
            public bool IsValid;
            public Vector3 Position;
            public Quaternion Rotation;
            public float SurfaceHeight;
            public float SurfaceAngle;
        }

        private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];
        private readonly FootState leftFoot = new();
        private readonly FootState rightFoot = new();

        private Animator animator;
        private CharacterController characterController;
        private PlayerRagdollController ragdollController;
        private FootIkConfig config;
        private float smoothedPelvisOffset;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ragdollController = GetComponentInParent<PlayerRagdollController>();
        }

        [Inject]
        public void Construct(FootIkConfig value, CharacterController ownerCharacterController)
        {
            config = value;
            characterController = ownerCharacterController;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!CanApplyIk())
            {
                FadeOutIk();
                return;
            }

            var rootUp = animator.transform.up;
            var leftAnimatedPosition = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            var rightAnimatedPosition = animator.GetIKPosition(AvatarIKGoal.RightFoot);
            var leftAnimatedRotation = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            var rightAnimatedRotation = animator.GetIKRotation(AvatarIKGoal.RightFoot);

            var leftGround = TryGetGroundPose(leftAnimatedPosition, leftAnimatedRotation, rootUp);
            var rightGround = TryGetGroundPose(rightAnimatedPosition, rightAnimatedRotation, rootUp);
            if (!RequiresTerrainCorrection(leftGround, rightGround))
            {
                FadeOutIk();
                return;
            }

            var leftWeight = UpdateContactWeight(leftFoot, leftAnimatedPosition, rootUp);
            var rightWeight = UpdateContactWeight(rightFoot, rightAnimatedPosition, rootUp);
            ApplyFoot(AvatarIKGoal.LeftFoot, leftFoot, leftGround, leftWeight);
            ApplyFoot(AvatarIKGoal.RightFoot, rightFoot, rightGround, rightWeight);
            ApplyPelvisOffset(leftAnimatedPosition, leftGround, leftFoot.IkWeight, rightAnimatedPosition, rightGround,
                rightFoot.IkWeight, rootUp);
        }

        private bool CanApplyIk()
        {
            ragdollController ??= GetComponentInParent<PlayerRagdollController>();

            return config != null
                   && animator != null
                   && animator.isHuman
                   && animator.isActiveAndEnabled
                   && !animator.applyRootMotion
                   && characterController != null
                   && characterController.enabled
                   && characterController.isGrounded
                   && (ragdollController == null || !ragdollController.IsDeathRagdollActive);
        }

        private GroundPose TryGetGroundPose(Vector3 animatedPosition, Quaternion animatedRotation, Vector3 rootUp)
        {
            var footForward = Vector3.ProjectOnPlane(animatedRotation * Vector3.forward, rootUp);
            if (footForward.sqrMagnitude < 0.0001f)
            {
                footForward = Vector3.ProjectOnPlane(animator.transform.forward, rootUp);
            }

            footForward.Normalize();
            var halfSpan = config.FootProbeSpan * 0.5f;
            var heel = ProbeGround(animatedPosition - footForward * halfSpan, animatedPosition, rootUp, 1f);
            var centre = ProbeGround(animatedPosition, animatedPosition, rootUp, 0f);
            var toe = ProbeGround(animatedPosition + footForward * halfSpan, animatedPosition, rootUp, 1f);

            if (CountValid(heel, centre, toe) < config.MinimumGroundProbes)
            {
                return default;
            }

            var bestProbe = SelectBestProbe(heel, centre, toe);
            var normal = AccumulateCompatibleNormal(heel, bestProbe, rootUp)
                         + AccumulateCompatibleNormal(centre, bestProbe, rootUp)
                         + AccumulateCompatibleNormal(toe, bestProbe, rootUp);
            if (normal.sqrMagnitude < 0.0001f)
            {
                return default;
            }

            normal.Normalize();
            var desiredRotation = Quaternion.FromToRotation(rootUp, normal) * animatedRotation;
            var rotationOffset = Quaternion.Angle(animatedRotation, desiredRotation);
            var rotation = rotationOffset > config.MaximumFootRotationAngle
                ? Quaternion.Slerp(animatedRotation, desiredRotation, config.MaximumFootRotationAngle / rotationOffset)
                : desiredRotation;

            return new GroundPose
            {
                IsValid = true,
                Position = bestProbe.Hit.point + normal * config.FootHeight,
                Rotation = rotation,
                SurfaceHeight = Vector3.Dot(bestProbe.Hit.point, rootUp),
                SurfaceAngle = Vector3.Angle(rootUp, normal)
            };
        }

        private GroundProbe ProbeGround(Vector3 probePosition, Vector3 animatedPosition, Vector3 rootUp,
                                        float centreDistancePenalty)
        {
            var hitCount = Physics.SphereCastNonAlloc(probePosition + rootUp * config.RayStartOffset,
                config.FootRadius, -rootUp, hitBuffer, config.RayDistance, config.GroundLayers,
                QueryTriggerInteraction.Ignore);
            var result = default(GroundProbe);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = hitBuffer[index];
                if (hit.collider == null || hit.collider.transform.IsChildOf(characterController.transform))
                {
                    continue;
                }

                var verticalOffset = Mathf.Abs(Vector3.Dot(hit.point - animatedPosition, rootUp));
                if (verticalOffset > config.MaximumFootOffset)
                {
                    continue;
                }

                var quality = hit.distance + centreDistancePenalty * config.ProbeEdgePenalty
                              + Vector3.Angle(rootUp, hit.normal) * config.ProbeSlopePenalty;
                if (!result.IsValid || quality < result.Quality)
                {
                    result = new GroundProbe { IsValid = true, Hit = hit, Quality = quality };
                }
            }

            return result;
        }

        private static int CountValid(GroundProbe heel, GroundProbe centre, GroundProbe toe) =>
            (heel.IsValid ? 1 : 0) + (centre.IsValid ? 1 : 0) + (toe.IsValid ? 1 : 0);

        private static GroundProbe SelectBestProbe(GroundProbe heel, GroundProbe centre, GroundProbe toe)
        {
            var best = default(GroundProbe);
            SelectBetterProbe(heel, ref best);
            SelectBetterProbe(centre, ref best);
            SelectBetterProbe(toe, ref best);
            return best;
        }

        private static void SelectBetterProbe(GroundProbe candidate, ref GroundProbe best)
        {
            if (candidate.IsValid && (!best.IsValid || candidate.Quality < best.Quality))
            {
                best = candidate;
            }
        }

        private Vector3 AccumulateCompatibleNormal(GroundProbe probe, GroundProbe bestProbe, Vector3 rootUp)
        {
            if (!probe.IsValid)
            {
                return Vector3.zero;
            }

            var heightDelta = Mathf.Abs(Vector3.Dot(probe.Hit.point - bestProbe.Hit.point, rootUp));
            return heightDelta <= config.MaximumProbeHeightDifference ? probe.Hit.normal : Vector3.zero;
        }

        private bool RequiresTerrainCorrection(GroundPose leftGround, GroundPose rightGround)
        {
            if (!leftGround.IsValid || !rightGround.IsValid)
            {
                return false;
            }

            return leftGround.SurfaceAngle > config.FlatSurfaceAngleTolerance
                   || rightGround.SurfaceAngle > config.FlatSurfaceAngleTolerance
                   || Mathf.Abs(leftGround.SurfaceHeight - rightGround.SurfaceHeight) > config.FlatSurfaceHeightTolerance;
        }

        private float UpdateContactWeight(FootState state, Vector3 animatedPosition, Vector3 rootUp)
        {
            var height = Vector3.Dot(animatedPosition - characterController.transform.position, rootUp);
            if (!state.HasHeightBaseline)
            {
                state.HeightBaseline = height;
                state.HasHeightBaseline = true;
            }

            var baselineSpeed = height < state.HeightBaseline ? 24f : 1.5f;
            state.HeightBaseline = Mathf.Lerp(state.HeightBaseline, height, ExponentialLerp(baselineSpeed));
            var lift = Mathf.Max(0f, height - state.HeightBaseline);
            var targetWeight = 1f - Mathf.InverseLerp(config.FootLiftStart, config.FootLiftEnd, lift);
            state.ContactWeight = Mathf.Lerp(state.ContactWeight, targetWeight, ExponentialLerp(config.ContactWeightSpeed));
            return state.ContactWeight;
        }

        private void ApplyFoot(AvatarIKGoal goal, FootState state, GroundPose targetPose, float targetWeight)
        {
            if (targetPose.IsValid)
            {
                if (!state.HasSmoothedPose)
                {
                    state.SmoothedPosition = targetPose.Position;
                    state.SmoothedRotation = targetPose.Rotation;
                    state.HasSmoothedPose = true;
                }
                else
                {
                    var interpolation = ExponentialLerp(config.InterpolationSpeed);
                    state.SmoothedPosition = Vector3.Lerp(state.SmoothedPosition, targetPose.Position, interpolation);
                    state.SmoothedRotation = Quaternion.Slerp(state.SmoothedRotation, targetPose.Rotation, interpolation);
                }
            }

            var requestedWeight = targetPose.IsValid ? Mathf.Clamp01(targetWeight) : 0f;
            var blendSpeed = requestedWeight > state.IkWeight ? config.IkEnterSpeed : config.IkExitSpeed;
            state.IkWeight = Mathf.Lerp(state.IkWeight, requestedWeight, ExponentialLerp(blendSpeed));

            animator.SetIKPositionWeight(goal, state.IkWeight);
            animator.SetIKRotationWeight(goal, state.IkWeight);
            if (state.HasSmoothedPose)
            {
                animator.SetIKPosition(goal, state.SmoothedPosition);
                animator.SetIKRotation(goal, state.SmoothedRotation);
            }

            if (state.IkWeight <= 0.001f && Mathf.Approximately(requestedWeight, 0f))
            {
                state.IkWeight = 0f;
                state.HasSmoothedPose = false;
            }
        }

        private void ApplyPelvisOffset(Vector3 leftAnimatedPosition, GroundPose leftPose, float leftWeight,
                                       Vector3 rightAnimatedPosition, GroundPose rightPose, float rightWeight,
                                       Vector3 rootUp)
        {
            var hasOffset = false;
            var lowestOffset = 0f;
            AccumulatePelvisOffset(leftAnimatedPosition, leftPose, leftWeight, rootUp, ref hasOffset, ref lowestOffset);
            AccumulatePelvisOffset(rightAnimatedPosition, rightPose, rightWeight, rootUp, ref hasOffset, ref lowestOffset);
            ApplyPelvisOffset(hasOffset ? Mathf.Clamp(lowestOffset, -config.PelvisMaximumOffset, config.PelvisMaximumOffset) : 0f,
                rootUp);
        }

        private void ApplyPelvisOffset(float targetOffset, Vector3 rootUp)
        {
            var blendSpeed = Mathf.Abs(targetOffset) > Mathf.Abs(smoothedPelvisOffset)
                ? config.IkEnterSpeed
                : config.IkExitSpeed;
            smoothedPelvisOffset = Mathf.Lerp(smoothedPelvisOffset, targetOffset, ExponentialLerp(blendSpeed));
            animator.bodyPosition += rootUp * smoothedPelvisOffset;
        }

        private static void AccumulatePelvisOffset(Vector3 animatedPosition, GroundPose pose, float weight,
                                                   Vector3 rootUp, ref bool hasOffset, ref float lowestOffset)
        {
            if (!pose.IsValid || Mathf.Approximately(weight, 0f))
            {
                return;
            }

            var offset = Vector3.Dot(pose.Position - animatedPosition, rootUp) * weight;
            if (!hasOffset || offset < lowestOffset)
            {
                lowestOffset = offset;
                hasOffset = true;
            }
        }

        private void FadeOutIk()
        {
            ApplyFoot(AvatarIKGoal.LeftFoot, leftFoot, default, 0f);
            ApplyFoot(AvatarIKGoal.RightFoot, rightFoot, default, 0f);
            ApplyPelvisOffset(0f, animator != null ? animator.transform.up : Vector3.up);
        }

        private static float ExponentialLerp(float speed) => 1f - Mathf.Exp(-speed * Time.deltaTime);
    }
}
