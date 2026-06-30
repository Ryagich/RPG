using UnityEngine;
using VContainer;

namespace NPC
{
    public sealed class NpcVision : MonoBehaviour
    {
        private const float DefaultOriginHeight = 1.35f;
        private const int MinSegments = 6;
        private const float DegreesPerSegment = 5f;

        private static readonly Color VisionGizmoColor = new(1f, 0.86f, 0.12f, 0.8f);

        [SerializeField] private NpcVisionConfig config;
        [SerializeField] private Transform origin;

        public NpcVisionConfig Config => config;
        public float ViewDistance => config != null ? config.ViewDistance : 0f;
        public float ViewAngle => config != null ? config.ViewAngle : 0f;

        [Inject]
        public void Construct(NpcVisionConfig npcVisionConfig)
        {
            if (npcVisionConfig != null)
            {
                config = npcVisionConfig;
            }
        }

        public bool IsInView(Vector3 worldPosition)
        {
            if (config == null || config.ViewDistance <= 0f || config.ViewAngle <= 0f)
            {
                return false;
            }

            var originPosition = GetOriginPosition();
            var toTarget = worldPosition - originPosition;
            toTarget.y = 0f;

            var distanceSqr = toTarget.sqrMagnitude;
            if (distanceSqr > config.ViewDistance * config.ViewDistance)
            {
                return false;
            }

            if (distanceSqr <= Mathf.Epsilon)
            {
                return true;
            }

            if (config.ViewAngle >= 360f)
            {
                return true;
            }

            return Vector3.Angle(GetPlanarForward(), toTarget.normalized) <= config.ViewAngle * 0.5f;
        }

        private void OnDrawGizmos()
        {
            if (config != null && config.DrawVisionForAllNpcs)
            {
                DrawVisionGizmo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (config != null && !config.DrawVisionForAllNpcs)
            {
                DrawVisionGizmo();
            }
        }

        private Vector3 GetOriginPosition()
        {
            return origin != null ? origin.position : transform.position + Vector3.up * DefaultOriginHeight;
        }

        private Vector3 GetPlanarForward()
        {
            var source = origin != null ? origin.forward : transform.forward;
            source.y = 0f;
            return source.sqrMagnitude > Mathf.Epsilon ? source.normalized : Vector3.forward;
        }

        private void DrawVisionGizmo()
        {
            if (config == null || config.ViewDistance <= 0f || config.ViewAngle <= 0f)
            {
                return;
            }

            var previousColor = Gizmos.color;
            Gizmos.color = VisionGizmoColor;

            var originPosition = GetOriginPosition();
            var forward = GetPlanarForward();
            var angle = Mathf.Clamp(config.ViewAngle, 0f, 360f);
            var halfAngle = angle * 0.5f;
            var segments = Mathf.Max(MinSegments, Mathf.CeilToInt(angle / DegreesPerSegment));
            var previousPoint = Vector3.zero;
            var firstPoint = Vector3.zero;
            var lastPoint = Vector3.zero;

            for (var index = 0; index <= segments; index++)
            {
                var step = segments == 0 ? 0f : index / (float)segments;
                var currentAngle = -halfAngle + angle * step;
                var direction = Quaternion.AngleAxis(currentAngle, Vector3.up) * forward;
                var point = originPosition + direction * config.ViewDistance;

                if (index == 0)
                {
                    firstPoint = point;
                }
                else
                {
                    Gizmos.DrawLine(previousPoint, point);
                }

                previousPoint = point;
                lastPoint = point;
            }

            if (angle < 360f)
            {
                Gizmos.DrawLine(originPosition, firstPoint);
                Gizmos.DrawLine(originPosition, lastPoint);
            }

            Gizmos.color = previousColor;
        }
    }
}
