using UnityEngine;

namespace CharacterAnimation
{
    [CreateAssetMenu(fileName = "Foot IK Config", menuName = "configs/Animation/Foot IK Config")]
    public sealed class FootIkConfig : ScriptableObject
    {
        [Header("Surface detection")]
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.001f)] private float footRadius = 0.08f;
        [SerializeField, Min(0f)] private float footHeight = 0.04f;
        [SerializeField, Min(0f)] private float rayStartOffset = 0.45f;
        [SerializeField, Min(0.01f)] private float rayDistance = 1.2f;
        [SerializeField, Min(0f)] private float maximumFootOffset = 0.65f;
        [SerializeField, Min(0.01f)] private float footProbeSpan = 0.22f;
        [SerializeField, Range(1, 3)] private int minimumGroundProbes = 2;
        [SerializeField, Min(0f)] private float maximumProbeHeightDifference = 0.18f;
        [SerializeField, Min(0f)] private float probeEdgePenalty = 0.05f;
        [SerializeField, Min(0f)] private float probeSlopePenalty = 0.002f;

        [Header("Activation")]
        [SerializeField, Min(0f)] private float flatSurfaceHeightTolerance = 0.025f;
        [SerializeField, Range(0f, 45f)] private float flatSurfaceAngleTolerance = 1f;
        [SerializeField, Min(0f)] private float footLiftStart = 0.025f;
        [SerializeField, Min(0.001f)] private float footLiftEnd = 0.12f;
        [SerializeField, Range(0f, 60f)] private float maximumFootRotationAngle = 25f;

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float interpolationSpeed = 18f;
        [SerializeField, Min(0f)] private float contactWeightSpeed = 16f;
        [SerializeField, Min(0f)] private float ikEnterSpeed = 10f;
        [SerializeField, Min(0f)] private float ikExitSpeed = 14f;
        [SerializeField, Min(0f)] private float pelvisMaximumOffset = 0.3f;

        public LayerMask GroundLayers => groundLayers;
        public float FootRadius => footRadius;
        public float FootHeight => footHeight;
        public float RayStartOffset => rayStartOffset;
        public float RayDistance => rayDistance;
        public float MaximumFootOffset => maximumFootOffset;
        public float FootProbeSpan => footProbeSpan;
        public int MinimumGroundProbes => minimumGroundProbes;
        public float MaximumProbeHeightDifference => maximumProbeHeightDifference;
        public float ProbeEdgePenalty => probeEdgePenalty;
        public float ProbeSlopePenalty => probeSlopePenalty;
        public float FlatSurfaceHeightTolerance => flatSurfaceHeightTolerance;
        public float FlatSurfaceAngleTolerance => flatSurfaceAngleTolerance;
        public float FootLiftStart => footLiftStart;
        public float FootLiftEnd => Mathf.Max(footLiftStart + 0.001f, footLiftEnd);
        public float MaximumFootRotationAngle => maximumFootRotationAngle;
        public float InterpolationSpeed => interpolationSpeed;
        public float ContactWeightSpeed => contactWeightSpeed;
        public float IkEnterSpeed => ikEnterSpeed;
        public float IkExitSpeed => ikExitSpeed;
        public float PelvisMaximumOffset => pelvisMaximumOffset;
    }
}
