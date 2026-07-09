using System;
using UnityEngine;

namespace Landings.Fields
{
    [Serializable]
    public sealed class FieldFurrow
    {
        [SerializeField] private GameObject furrowObject;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;
        [SerializeField, Min(0.1f)] private float fallbackLength = 4f;
        [SerializeField] private FurrowAxis fallbackAxis = FurrowAxis.Forward;

        public GameObject FurrowObject => furrowObject;
        public float Length => Vector3.Distance(StartPosition, EndPosition);

        private Vector3 StartPosition => startPoint != null ? startPoint.position : BoundsCenter - Direction * (ResolvedLength * 0.5f);
        private Vector3 EndPosition => endPoint != null ? endPoint.position : BoundsCenter + Direction * (ResolvedLength * 0.5f);
        private Vector3 BoundsCenter => TryGetBounds(out var bounds) ? bounds.center : Center;
        private Vector3 Center => furrowObject != null ? furrowObject.transform.position : Vector3.zero;
        private Transform FallbackTransform => furrowObject != null ? furrowObject.transform : null;
        private float ResolvedLength => TryGetBounds(out var bounds) ? GetProjectedLength(bounds, Direction) : Mathf.Max(0.1f, fallbackLength);

        public Vector3 GetPoint(float normalizedPosition, float sideOffset)
        {
            var point = Vector3.Lerp(StartPosition, EndPosition, Mathf.Clamp01(normalizedPosition));
            return point + SideDirection * sideOffset;
        }

        private Vector3 Direction
        {
            get
            {
                if (startPoint != null && endPoint != null)
                {
                    var direction = endPoint.position - startPoint.position;
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        return direction.normalized;
                    }
                }

                if (FallbackTransform == null)
                {
                    return Vector3.forward;
                }

                return fallbackAxis == FurrowAxis.Right
                    ? FallbackTransform.right
                    : FallbackTransform.forward;
            }
        }

        private Vector3 SideDirection
        {
            get
            {
                var side = Vector3.Cross(Vector3.up, Direction);
                return side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.right;
            }
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            if (furrowObject == null)
            {
                return false;
            }

            var hasBounds = false;
            foreach (var renderer in furrowObject.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            if (hasBounds)
            {
                return true;
            }

            foreach (var collider in furrowObject.GetComponentsInChildren<Collider>())
            {
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(collider.bounds);
            }

            return hasBounds;
        }

        private static float GetProjectedLength(Bounds bounds, Vector3 direction)
        {
            direction.Normalize();
            var extents = bounds.extents;
            var halfLength = Mathf.Abs(direction.x) * extents.x
                           + Mathf.Abs(direction.y) * extents.y
                           + Mathf.Abs(direction.z) * extents.z;
            return Mathf.Max(0.1f, halfLength * 2f);
        }

        private enum FurrowAxis
        {
            Forward,
            Right
        }
    }
}
