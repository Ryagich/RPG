using UnityEngine;
using UnityEngine.AI;
using VContainer.Unity;

namespace Monsters
{
    /// <summary>
    /// Owns PlantMonster_1 navigation. It is target-agnostic: states decide where to move,
    /// while this controller validates paths, drives the agent, and reports locomotion.
    /// </summary>
    public sealed class PlantMonsterNavMeshController : IStartable, ITickable
    {
        private const float DefaultSampleRadius = 2f;
        private const float VelocityThreshold = 0.01f;
        // Moving targets can change their transform every frame. Rebuilding a complete
        // NavMesh path for such a sub-frame change is disproportionately expensive, so
        // navigation refreshes at a bounded cadence shared with NPC navigation.
        private const float DestinationReuseDistance = 0.25f;
        private const float PathRefreshInterval = 0.15f;

        private readonly NavMeshAgent agent;
        private readonly PlantMonsterAnimationController animation;
        private readonly float defaultStoppingDistance;
        private readonly float defaultSpeed;

        private bool isMovementLocked;
        private bool isNavigationDisabled;
        private bool isInitialized;
        private bool hasMoveRequest;
        private Vector3 lastDestination;
        private float lastStoppingDistance;
        private float nextPathRefreshTime;

        public PlantMonsterNavMeshController(NavMeshAgent agent, PlantMonsterAnimationController animation)
        {
            this.agent = agent;
            this.animation = animation;
            defaultStoppingDistance = agent != null ? agent.stoppingDistance : 0f;
            defaultSpeed = agent != null ? agent.speed : 0f;
        }

        public Vector3 Velocity => IsAgentOnNavMesh ? agent.velocity : Vector3.zero;
        public bool HasPath => IsAgentOnNavMesh && agent.hasPath;
        public bool IsMoving => IsAgentOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > VelocityThreshold;
        public bool IsMovementLocked => isMovementLocked;

        public bool HasReachedDestination => IsAgentOnNavMesh
                                             && !agent.pathPending
                                             && agent.remainingDistance <= agent.stoppingDistance;

        public void Start() => EnsureAgentIsReady();

        public void Tick()
        {
            if (!EnsureAgentIsReady())
            {
                animation.SetLocomotion(0f);
                return;
            }

            var planarVelocity = agent.velocity;
            planarVelocity.y = 0f;
            var speedRatio = defaultSpeed <= VelocityThreshold ? 0f : planarVelocity.magnitude / defaultSpeed;
            animation.SetLocomotion(speedRatio);
        }

        public bool MoveTo(Vector3 destination, float sampleRadius = DefaultSampleRadius, float? stoppingDistance = null)
        {
            if (isMovementLocked || !EnsureAgentIsReady())
            {
                return false;
            }

            var resolvedStoppingDistance = Mathf.Max(0f, stoppingDistance ?? defaultStoppingDistance);
            if (CanReuseCurrentPath(destination, resolvedStoppingDistance))
            {
                agent.isStopped = false;
                agent.stoppingDistance = resolvedStoppingDistance;
                return true;
            }

            if (!NavMesh.SamplePosition(destination, out var hit, sampleRadius, NavMesh.AllAreas)
                || !TryCalculatePath(hit.position, out var path))
            {
                return false;
            }

            agent.stoppingDistance = resolvedStoppingDistance;
            agent.isStopped = false;
            if (!agent.SetPath(path))
            {
                return false;
            }

            hasMoveRequest = true;
            lastDestination = destination;
            lastStoppingDistance = resolvedStoppingDistance;
            nextPathRefreshTime = Time.time + PathRefreshInterval;
            return true;
        }

        public void Stop()
        {
            if (IsAgentOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            ClearMoveRequest();
            animation.SetLocomotion(0f);
        }

        /// <summary>Use this while an action animation must prevent navigation movement.</summary>
        public void SetMovementLocked(bool isLocked)
        {
            if (isMovementLocked == isLocked)
            {
                return;
            }

            isMovementLocked = isLocked;
            if (isLocked)
            {
                Stop();
            }
        }

        public void Disable()
        {
            isNavigationDisabled = true;
            Stop();
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        public void ResetSpeed()
        {
            if (agent != null)
            {
                agent.speed = defaultSpeed;
            }
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            if (agent != null)
            {
                agent.speed = Mathf.Max(0.01f, defaultSpeed * Mathf.Max(0.01f, multiplier));
            }
        }

        private bool EnsureAgentIsReady()
        {
            if (agent == null || isNavigationDisabled)
            {
                return false;
            }

            if (!agent.enabled)
            {
                if (!NavMesh.SamplePosition(agent.transform.position, out var hit, DefaultSampleRadius, NavMesh.AllAreas))
                {
                    return false;
                }

                agent.enabled = true;
                if (!agent.isOnNavMesh || !agent.Warp(hit.position))
                {
                    agent.enabled = false;
                    return false;
                }
            }

            if (!isInitialized)
            {
                agent.avoidancePriority = Random.Range(0, 100);
                agent.updatePosition = true;
                agent.updateRotation = true;
                isInitialized = true;
            }

            return agent.isOnNavMesh || WarpToNearestNavMesh();
        }

        private bool TryCalculatePath(Vector3 destination, out NavMeshPath path)
        {
            path = new NavMeshPath();
            return NavMesh.CalculatePath(agent.transform.position, destination, NavMesh.AllAreas, path)
                   && path.status == NavMeshPathStatus.PathComplete
                   && path.corners != null
                   && path.corners.Length > 0;
        }

        private bool WarpToNearestNavMesh()
        {
            return NavMesh.SamplePosition(agent.transform.position, out var hit, DefaultSampleRadius, NavMesh.AllAreas)
                   && agent.Warp(hit.position);
        }

        private bool CanReuseCurrentPath(Vector3 destination, float stoppingDistance)
        {
            if (!hasMoveRequest || !IsAgentOnNavMesh || agent.pathPending || !agent.hasPath || agent.isStopped)
            {
                return false;
            }

            var delta = destination - lastDestination;
            delta.y = 0f;
            if (Mathf.Abs(stoppingDistance - lastStoppingDistance) > 0.01f)
            {
                return false;
            }

            return delta.sqrMagnitude <= DestinationReuseDistance * DestinationReuseDistance
                   || Time.time < nextPathRefreshTime;
        }

        private void ClearMoveRequest()
        {
            hasMoveRequest = false;
            lastDestination = default;
            lastStoppingDistance = default;
            nextPathRefreshTime = 0f;
        }

        private bool IsAgentOnNavMesh => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }
}
