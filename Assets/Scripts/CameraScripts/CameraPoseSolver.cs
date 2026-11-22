using UnityEngine;

namespace CameraScripts
{
    /// <summary>
    /// Shared low-level camera pose math. Individual camera modes own target selection while this
    /// class owns look rotation, damping and obstacle resolution.
    /// </summary>
    internal sealed class CameraPoseSolver
    {
        private const int MaxCollisionHits = 16;

        private readonly RaycastHit[] collisionHits = new RaycastHit[MaxCollisionHits];

        public Quaternion GetLookRotation(Vector3 lookPoint, Vector3 cameraPosition, Quaternion fallbackRotation)
        {
            var direction = lookPoint - cameraPosition;
            return direction.sqrMagnitude <= Mathf.Epsilon
                ? fallbackRotation
                : Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        public Vector3 ResolveCollision(
            Vector3 pivotPosition,
            Vector3 desiredPosition,
            LayerMask collisionLayers,
            float collisionRadius,
            float collisionPadding,
            Transform firstIgnoredRoot,
            Transform secondIgnoredRoot = null)
        {
            var pivotToCamera = desiredPosition - pivotPosition;
            var desiredDistance = pivotToCamera.magnitude;
            if (desiredDistance <= Mathf.Epsilon)
            {
                return desiredPosition;
            }

            var direction = pivotToCamera / desiredDistance;
            var hitCount = collisionRadius > 0f
                ? Physics.SphereCastNonAlloc(
                    pivotPosition,
                    collisionRadius,
                    direction,
                    collisionHits,
                    desiredDistance,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(
                    pivotPosition,
                    direction,
                    collisionHits,
                    desiredDistance,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore);

            var firstRoot = firstIgnoredRoot != null ? firstIgnoredRoot.root : null;
            var secondRoot = secondIgnoredRoot != null ? secondIgnoredRoot.root : null;
            var closestDistance = desiredDistance;
            for (var i = 0; i < hitCount; i++)
            {
                var hit = collisionHits[i];
                if (hit.collider == null || hit.distance >= closestDistance)
                {
                    continue;
                }

                var colliderRoot = hit.collider.transform.root;
                if (colliderRoot == firstRoot || colliderRoot == secondRoot)
                {
                    continue;
                }

                closestDistance = hit.distance;
            }

            return closestDistance >= desiredDistance
                ? desiredPosition
                : pivotPosition + direction * Mathf.Max(0f, closestDistance - collisionPadding);
        }

        public static Vector3 SmoothPosition(
            Vector3 current,
            Vector3 target,
            ref Vector3 velocity,
            float smoothTime,
            float deltaTime)
        {
            return Vector3.SmoothDamp(
                current,
                target,
                ref velocity,
                Mathf.Max(0f, smoothTime),
                Mathf.Infinity,
                Mathf.Max(0f, deltaTime));
        }

        public static Quaternion SmoothRotation(
            Quaternion current,
            Quaternion target,
            ref Vector4 velocity,
            float smoothTime,
            float deltaTime)
        {
        var effectiveSmoothTime = Mathf.Max(0f, smoothTime);
        var effectiveDeltaTime = Mathf.Max(0f, deltaTime);

            if (Quaternion.Dot(current, target) < 0f)
            {
                target = new Quaternion(-target.x, -target.y, -target.z, -target.w);
            }

            var rotation = new Quaternion(
            Mathf.SmoothDamp(current.x, target.x, ref velocity.x, effectiveSmoothTime, Mathf.Infinity, effectiveDeltaTime),
            Mathf.SmoothDamp(current.y, target.y, ref velocity.y, effectiveSmoothTime, Mathf.Infinity, effectiveDeltaTime),
            Mathf.SmoothDamp(current.z, target.z, ref velocity.z, effectiveSmoothTime, Mathf.Infinity, effectiveDeltaTime),
            Mathf.SmoothDamp(current.w, target.w, ref velocity.w, effectiveSmoothTime, Mathf.Infinity, effectiveDeltaTime));
            var magnitude = Mathf.Sqrt(
                rotation.x * rotation.x
                + rotation.y * rotation.y
                + rotation.z * rotation.z
                + rotation.w * rotation.w);

            return magnitude <= Mathf.Epsilon
                ? target
                : new Quaternion(
                    rotation.x / magnitude,
                    rotation.y / magnitude,
                    rotation.z / magnitude,
                    rotation.w / magnitude);
        }
    }
}
