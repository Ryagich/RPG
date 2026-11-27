using UnityEngine;

namespace CameraScripts
{
    /// <summary>
    /// Routes a camera transition around the player only when its direct segment crosses the
    /// player's protected capsule. The route reaches its clearance circle, follows the circle,
    /// then exits toward the destination; either endpoint may be inside the extra clearance.
    /// </summary>
    internal sealed class CameraTransitionArc
    {
        private const float GeometryEpsilon = 0.0001f;

        private readonly CharacterController playerCharacterController;

        private Vector3 startPosition;
        private float progress;
        private float progressVelocity;
        private bool isActive;
        private int routeDirection;

        public CameraTransitionArc(CharacterController playerCharacterController)
        {
            this.playerCharacterController = playerCharacterController;
        }

        public bool TryGetNextPosition(
            Vector3 currentPosition,
            Vector3 desiredPosition,
            float smoothTime,
            float deltaTime,
            float playerAvoidanceOffset,
            float completionProgress,
            out Vector3 nextPosition)
        {
            if (playerCharacterController == null)
            {
                nextPosition = desiredPosition;
                return false;
            }

            var capsule = GetPlayerCapsule();
            var protectedRadius = capsule.Radius + Mathf.Max(0f, playerAvoidanceOffset);

            if (!isActive)
            {
                // Only a path through the CharacterController needs a detour. The dialog pose
                // itself may legitimately finish inside the optional offset, so treating the
                // expanded radius as an obstacle here would restart an arc after it settled.
                if (!IntersectsPlayerCapsule(currentPosition, desiredPosition, capsule, capsule.Radius))
                {
                    nextPosition = desiredPosition;
                    return false;
                }

                startPosition = currentPosition;
                progress = 0f;
                progressVelocity = 0f;
                if (!TryBuildRoute(
                        startPosition,
                        desiredPosition,
                        capsule.Center,
                        protectedRadius,
                        preferredDirection: 0,
                        out var initialRoute))
                {
                    nextPosition = desiredPosition;
                    return false;
                }

                routeDirection = initialRoute.Direction;
                isActive = true;
            }

            progress = Mathf.SmoothDamp(
                progress,
                1f,
                ref progressVelocity,
                Mathf.Max(0.01f, smoothTime),
                Mathf.Infinity,
                Mathf.Max(0f, deltaTime));

            nextPosition = TryBuildRoute(
                startPosition,
                desiredPosition,
                capsule.Center,
                protectedRadius,
                routeDirection,
                out var route)
                ? route.GetPosition(progress)
                : GetFallbackArcPosition(
                    startPosition,
                    desiredPosition,
                    capsule.Center,
                    protectedRadius,
                    routeDirection,
                    progress);

            if (progress < Mathf.Clamp(completionProgress, 0.9f, 1f))
            {
                return true;
            }

            nextPosition = desiredPosition;
            Reset();
            return true;
        }

        public void Reset()
        {
            startPosition = default;
            progress = 0f;
            progressVelocity = 0f;
            isActive = false;
            routeDirection = 0;
        }

        private static bool TryBuildRoute(
            Vector3 start,
            Vector3 destination,
            Vector3 capsuleCenter,
            float protectedRadius,
            int preferredDirection,
            out AvoidanceRoute route)
        {
            var center = Flatten(capsuleCenter);
            var planarStart = Flatten(start);
            var planarDestination = Flatten(destination);
            var hasCandidate = false;
            var shortestLength = float.PositiveInfinity;
            route = default;

            var hasStartTangents = TryGetTangentAngles(
                planarStart,
                center,
                protectedRadius,
                out var startFirstAngle,
                out var startSecondAngle);
            var startCount = hasStartTangents ? 2 : 1;
            var radialStartAngle = GetPlanarAngle(planarStart - center, Vector3.back);
            var hasEndTangents = TryGetTangentAngles(
                planarDestination,
                center,
                protectedRadius,
                out var endFirstAngle,
                out var endSecondAngle);
            var endCount = hasEndTangents ? 2 : 1;
            var radialEndAngle = GetPlanarAngle(planarDestination - center, Vector3.back);

            for (var startIndex = 0; startIndex < startCount; startIndex++)
            {
                for (var endIndex = 0; endIndex < endCount; endIndex++)
                {
                    var entryAngle = hasStartTangents
                        ? startIndex == 0 ? startFirstAngle : startSecondAngle
                        : radialStartAngle;
                    var exitAngle = hasEndTangents
                        ? endIndex == 0 ? endFirstAngle : endSecondAngle
                        : radialEndAngle;
                    var signedArcDegrees = Mathf.DeltaAngle(entryAngle, exitAngle);
                    var direction = GetDirection(signedArcDegrees);
                    if (preferredDirection != 0 && direction != preferredDirection)
                    {
                        continue;
                    }

                    var entryPoint = PointOnCircle(center, protectedRadius, entryAngle);
                    var exitPoint = PointOnCircle(center, protectedRadius, exitAngle);
                    var entryLength = Vector3.Distance(planarStart, entryPoint);
                    var arcLength = Mathf.Abs(signedArcDegrees) * Mathf.Deg2Rad * protectedRadius;
                    var exitLength = Vector3.Distance(exitPoint, planarDestination);
                    var totalLength = entryLength + arcLength + exitLength;

                    if (totalLength >= shortestLength)
                    {
                        continue;
                    }

                    shortestLength = totalLength;
                    route = new AvoidanceRoute(
                        start,
                        destination,
                        center,
                        protectedRadius,
                        entryAngle,
                        signedArcDegrees,
                        entryLength,
                        arcLength,
                        exitLength);
                    hasCandidate = true;
                }
            }

            return hasCandidate;
        }

        private static bool TryGetTangentAngles(
            Vector3 point,
            Vector3 center,
            float radius,
            out float firstAngle,
            out float secondAngle)
        {
            var offset = point - center;
            var distance = offset.magnitude;
            if (distance <= radius + GeometryEpsilon)
            {
                firstAngle = 0f;
                secondAngle = 0f;
                return false;
            }

            var pointAngle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
            var tangentOffset = Mathf.Acos(Mathf.Clamp(radius / distance, -1f, 1f)) * Mathf.Rad2Deg;
            firstAngle = pointAngle + tangentOffset;
            secondAngle = pointAngle - tangentOffset;
            return true;
        }

        private static Vector3 GetFallbackArcPosition(
            Vector3 start,
            Vector3 destination,
            Vector3 capsuleCenter,
            float protectedRadius,
            int preferredDirection,
            float t)
        {
            var center = Flatten(capsuleCenter);
            var startOffset = Flatten(start) - center;
            var endOffset = Flatten(destination) - center;
            var startDirection = GetPlanarDirection(startOffset, Vector3.back);
            var endDirection = GetPlanarDirection(endOffset, startDirection);
            var startAngle = Mathf.Atan2(startDirection.z, startDirection.x) * Mathf.Rad2Deg;
            var endAngle = Mathf.Atan2(endDirection.z, endDirection.x) * Mathf.Rad2Deg;
            var signedAngle = Mathf.DeltaAngle(startAngle, endAngle);
            if (preferredDirection > 0 && signedAngle < 0f)
            {
                signedAngle += 360f;
            }
            else if (preferredDirection < 0 && signedAngle > 0f)
            {
                signedAngle -= 360f;
            }

            var angle = startAngle + signedAngle * t;
            var startRadius = Mathf.Max(protectedRadius, startOffset.magnitude);
            var endRadius = Mathf.Max(protectedRadius, endOffset.magnitude);
            var point = PointOnCircle(center, Mathf.Lerp(startRadius, endRadius, t), angle);
            point.y = Mathf.Lerp(start.y, destination.y, t);
            return point;
        }

        private static bool IntersectsPlayerCapsule(
            Vector3 start,
            Vector3 end,
            PlayerCapsule capsule,
            float protectedRadius)
        {
            return SegmentToSegmentDistanceSquared(start, end, capsule.Bottom, capsule.Top)
                   <= protectedRadius * protectedRadius;
        }

        private PlayerCapsule GetPlayerCapsule()
        {
            var playerTransform = playerCharacterController.transform;
            var scale = playerTransform.lossyScale;
            var horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var radius = playerCharacterController.radius * horizontalScale;
            var height = Mathf.Max(playerCharacterController.height * Mathf.Abs(scale.y), radius * 2f);
            var center = playerTransform.TransformPoint(playerCharacterController.center);
            var halfSegmentHeight = Mathf.Max(0f, height * 0.5f - radius);
            return new PlayerCapsule(
                center - Vector3.up * halfSegmentHeight,
                center + Vector3.up * halfSegmentHeight,
                radius);
        }

        private static Vector3 Flatten(Vector3 point)
        {
            point.y = 0f;
            return point;
        }

        private static Vector3 PointOnCircle(Vector3 center, float radius, float angleDegrees)
        {
            var angleRadians = angleDegrees * Mathf.Deg2Rad;
            return center + new Vector3(
                Mathf.Cos(angleRadians) * radius,
                0f,
                Mathf.Sin(angleRadians) * radius);
        }

        private static Vector3 GetPlanarDirection(Vector3 vector, Vector3 fallback)
        {
            vector.y = 0f;
            if (vector.sqrMagnitude > Mathf.Epsilon)
            {
                return vector.normalized;
            }

            fallback.y = 0f;
            return fallback.sqrMagnitude > Mathf.Epsilon ? fallback.normalized : Vector3.back;
        }

        private static float GetPlanarAngle(Vector3 vector, Vector3 fallback)
        {
            var direction = GetPlanarDirection(vector, fallback);
            return Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
        }

        private static int GetDirection(float signedAngle)
        {
            if (signedAngle > GeometryEpsilon)
            {
                return 1;
            }

            return signedAngle < -GeometryEpsilon ? -1 : 0;
        }

        private static float SegmentToSegmentDistanceSquared(Vector3 firstStart, Vector3 firstEnd, Vector3 secondStart, Vector3 secondEnd)
        {
            var firstDirection = firstEnd - firstStart;
            var secondDirection = secondEnd - secondStart;
            var betweenStarts = firstStart - secondStart;
            var firstLengthSquared = Vector3.Dot(firstDirection, firstDirection);
            var secondLengthSquared = Vector3.Dot(secondDirection, secondDirection);
            var directionsDot = Vector3.Dot(firstDirection, secondDirection);
            var firstProjection = Vector3.Dot(firstDirection, betweenStarts);
            var secondProjection = Vector3.Dot(secondDirection, betweenStarts);
            var denominator = firstLengthSquared * secondLengthSquared - directionsDot * directionsDot;

            float firstParameter;
            float secondParameter;
            if (firstLengthSquared <= Mathf.Epsilon && secondLengthSquared <= Mathf.Epsilon)
            {
                return betweenStarts.sqrMagnitude;
            }

            if (firstLengthSquared <= Mathf.Epsilon)
            {
                firstParameter = 0f;
                secondParameter = Mathf.Clamp01(secondProjection / secondLengthSquared);
            }
            else if (secondLengthSquared <= Mathf.Epsilon)
            {
                firstParameter = Mathf.Clamp01(-firstProjection / firstLengthSquared);
                secondParameter = 0f;
            }
            else
            {
                firstParameter = denominator > Mathf.Epsilon
                    ? Mathf.Clamp01((directionsDot * secondProjection - firstProjection * secondLengthSquared) / denominator)
                    : 0f;
                secondParameter = Mathf.Clamp01((directionsDot * firstParameter + secondProjection) / secondLengthSquared);
                firstParameter = Mathf.Clamp01((directionsDot * secondParameter - firstProjection) / firstLengthSquared);
            }

            var difference = betweenStarts + firstDirection * firstParameter - secondDirection * secondParameter;
            return difference.sqrMagnitude;
        }

        private readonly struct AvoidanceRoute
        {
            private readonly Vector3 start;
            private readonly Vector3 destination;
            private readonly Vector3 center;
            private readonly float radius;
            private readonly float entryAngle;
            private readonly float signedArcDegrees;
            private readonly float entryLength;
            private readonly float arcLength;
            private readonly float exitLength;
            private readonly float totalLength;

            public AvoidanceRoute(
                Vector3 start,
                Vector3 destination,
                Vector3 center,
                float radius,
                float entryAngle,
                float signedArcDegrees,
                float entryLength,
                float arcLength,
                float exitLength)
            {
                this.start = start;
                this.destination = destination;
                this.center = center;
                this.radius = radius;
                this.entryAngle = entryAngle;
                this.signedArcDegrees = signedArcDegrees;
                this.entryLength = entryLength;
                this.arcLength = arcLength;
                this.exitLength = exitLength;
                totalLength = entryLength + arcLength + exitLength;
            }

            public Vector3 GetPosition(float routeProgress)
            {
                var travelledDistance = Mathf.Clamp01(routeProgress) * totalLength;
                Vector3 planarPosition;

                if (travelledDistance <= entryLength && entryLength > GeometryEpsilon)
                {
                    planarPosition = Vector3.Lerp(Flatten(start), PointOnCircle(center, radius, entryAngle), travelledDistance / entryLength);
                }
                else if (travelledDistance <= entryLength + arcLength && arcLength > GeometryEpsilon)
                {
                    var arcProgress = (travelledDistance - entryLength) / arcLength;
                    planarPosition = PointOnCircle(center, radius, entryAngle + signedArcDegrees * arcProgress);
                }
                else if (exitLength > GeometryEpsilon)
                {
                    var exitProgress = (travelledDistance - entryLength - arcLength) / exitLength;
                    var exitAngle = entryAngle + signedArcDegrees;
                    planarPosition = Vector3.Lerp(PointOnCircle(center, radius, exitAngle), Flatten(destination), exitProgress);
                }
                else
                {
                    planarPosition = Flatten(destination);
                }

                planarPosition.y = Mathf.Lerp(start.y, destination.y, Mathf.Clamp01(routeProgress));
                return planarPosition;
            }

            public int Direction => GetDirection(signedArcDegrees);
        }

        private readonly struct PlayerCapsule
        {
            public PlayerCapsule(Vector3 bottom, Vector3 top, float radius)
            {
                Bottom = bottom;
                Top = top;
                Radius = radius;
            }

            public Vector3 Bottom { get; }
            public Vector3 Top { get; }
            public float Radius { get; }
            public Vector3 Center => (Bottom + Top) * 0.5f;
        }
    }
}
