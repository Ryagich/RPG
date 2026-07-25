using UnityEngine;
using UnityEngine.AI;
using Stats;
using VContainer.Unity;

namespace NPC
{
    public sealed class NpcNavMeshController : IStartable, ITickable, IStaminaMovementState
    {
        private const float DefaultSampleRadius = 2f;
        private const float VelocityThreshold = 0.01f;
        private const float DestinationReuseDistance = 0.08f;
        private const string DirectionXParameter = "DirectionX";
        private const string DirectionYParameter = "DirectionY";
        private const string IsRunParameter = "IsRun";

        private readonly NavMeshAgent agent;
        private readonly CharacterController characterController;
        private readonly Animator animator;
        private readonly float defaultStoppingDistance;
        private readonly float defaultSpeed;

        private bool isFacingLocked;
        private bool isEvasionDirectionLocked;
        private bool hasMoveRequest;
        private Vector2 evasionDirectionalInput;
        private Vector3 lastRequestedDestination;
        private float lastRequestedStoppingDistance;

        public NpcNavMeshController(NavMeshAgent agent, CharacterController characterController, Animator animator)
        {
            this.agent = agent;
            this.characterController = characterController;
            this.animator = animator;
            defaultStoppingDistance = agent != null ? agent.stoppingDistance : 0f;
            defaultSpeed = agent != null ? agent.speed : 0f;
        }

        public Vector3 Velocity => agent != null ? agent.velocity : Vector3.zero;
        public bool HasPath => agent != null && agent.enabled && agent.hasPath;
        public bool IsMoving => agent != null
                                && agent.enabled
                                && !agent.isStopped
                                && agent.desiredVelocity.sqrMagnitude > VelocityThreshold;
        public bool IsRunning => agent != null && agent.speed > defaultSpeed + VelocityThreshold;
        public bool IsFacingLocked => isFacingLocked;

        public bool HasReachedDestination
        {
            get
            {
                if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending)
                {
                    return false;
                }

                return agent.remainingDistance <= agent.stoppingDistance;
            }
        }

        public void Start()
        {
            if (agent == null)
            {
                return;
            }

            agent.avoidancePriority = Random.Range(0, 100);
            agent.updatePosition = characterController == null;
            agent.updateRotation = characterController == null;

            if (!agent.isOnNavMesh)
            {
                WarpToNearestNavMesh(agent.transform.position);
            }

            agent.nextPosition = agent.transform.position;
        }

        public void Tick()
        {
            if (agent == null || !agent.enabled || characterController == null || !characterController.enabled)
            {
                UpdateAnimator(Vector3.zero);
                return;
            }

            if (!agent.isOnNavMesh)
            {
                WarpToNearestNavMesh(agent.transform.position);
                return;
            }

            agent.nextPosition = agent.transform.position;
            if (agent.isStopped || agent.pathPending || !agent.hasPath)
            {
                UpdateAnimator(Vector3.zero);
                return;
            }

            var desiredVelocity = agent.desiredVelocity;
            desiredVelocity.y = 0f;
            if (desiredVelocity.sqrMagnitude <= VelocityThreshold)
            {
                UpdateAnimator(Vector3.zero);
                return;
            }

            var facingDirection = GetFacingDirection(desiredVelocity);
            RotateToMovementDirection(facingDirection);

            var maxStep = Mathf.Max(0.01f, agent.speed) * Time.deltaTime;
            var displacement = Vector3.ClampMagnitude(desiredVelocity, maxStep);
            characterController.Move(displacement);
            agent.nextPosition = agent.transform.position;
            UpdateAnimator(displacement / Mathf.Max(Time.deltaTime, 0.0001f));
        }

        public bool MoveTo(Vector3 destination, float sampleRadius = DefaultSampleRadius, float? stoppingDistance = null)
        {
            if (!CanUseAgent())
            {
                return false;
            }

            if (!NavMesh.SamplePosition(destination, out var hit, sampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(agent.transform.position, hit.position, NavMesh.AllAreas, path)
             || path.status != NavMeshPathStatus.PathComplete
             || path.corners == null
             || path.corners.Length == 0)
            {
                return false;
            }

            var resolvedStoppingDistance = Mathf.Max(0f, stoppingDistance ?? defaultStoppingDistance);
            if (CanReuseCurrentPath(hit.position, resolvedStoppingDistance))
            {
                agent.isStopped = false;
                agent.stoppingDistance = resolvedStoppingDistance;
                return true;
            }

            agent.isStopped = false;
            agent.stoppingDistance = resolvedStoppingDistance;
            agent.updatePosition = characterController == null;
            agent.updateRotation = characterController == null;
            var didSetPath = agent.SetPath(path);
            if (didSetPath)
            {
                hasMoveRequest = true;
                lastRequestedDestination = hit.position;
                lastRequestedStoppingDistance = resolvedStoppingDistance;
            }

            return didSetPath;
        }

        public bool TryCalculateEta(Vector3 destination, out float eta, float sampleRadius = DefaultSampleRadius)
        {
            eta = 0f;
            if (!CanUseAgent())
            {
                return false;
            }

            if (!NavMesh.SamplePosition(destination, out var hit, sampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(agent.transform.position, hit.position, NavMesh.AllAreas, path)
             || path.status != NavMeshPathStatus.PathComplete
             || path.corners == null
             || path.corners.Length == 0)
            {
                return false;
            }

            var distance = 0f;
            for (var index = 1; index < path.corners.Length; index++)
            {
                distance += Vector3.Distance(path.corners[index - 1], path.corners[index]);
            }

            var speed = Mathf.Max(0.01f, agent.speed);
            eta = distance / speed;
            return true;
        }

        public void Stop()
        {
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            ClearMoveRequest();
            UpdateAnimator(Vector3.zero);
        }

        public void Disable()
        {
            Stop();

            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        public void Resume()
        {
            if (CanUseAgent())
            {
                agent.isStopped = false;
            }
        }

        public void SetFacingLocked(bool isLocked)
        {
            isFacingLocked = isLocked;
        }

        /// <summary>
        /// Keeps the directional parameters selected for a Dodge/Roll until its animation has
        /// released movement. NavMesh normally writes zero velocity while stopped, which would
        /// otherwise overwrite the blend-tree direction on the next frame.
        /// </summary>
        public void LockEvasionDirection(Vector3 worldDirection)
        {
            if (animator == null)
            {
                return;
            }

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= VelocityThreshold)
            {
                worldDirection = animator.transform.forward;
            }

            var localDirection = animator.transform.InverseTransformDirection(worldDirection.normalized);
            evasionDirectionalInput = new Vector2(
                Mathf.Clamp(localDirection.x, -1f, 1f),
                Mathf.Clamp(localDirection.z, -1f, 1f));
            isEvasionDirectionLocked = true;
            ApplyLockedEvasionDirection();
        }

        public void ReleaseEvasionDirection()
        {
            isEvasionDirectionLocked = false;
            evasionDirectionalInput = Vector2.zero;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            if (agent == null)
            {
                return;
            }

            agent.speed = Mathf.Max(0.01f, defaultSpeed * Mathf.Max(0.01f, multiplier));
        }

        public void ResetSpeed()
        {
            if (agent != null)
            {
                agent.speed = defaultSpeed;
            }
        }

        public bool WarpToNearestNavMesh(Vector3 position, float sampleRadius = DefaultSampleRadius)
        {
            if (agent == null || !agent.enabled)
            {
                return false;
            }

            return NavMesh.SamplePosition(position, out var hit, sampleRadius, NavMesh.AllAreas)
                && Warp(hit.position);
        }

        private bool Warp(Vector3 position)
        {
            ClearMoveRequest();
            return agent.Warp(position);
        }

        private bool CanUseAgent()
        {
            if (agent == null || !agent.enabled)
            {
                return false;
            }

            return agent.isOnNavMesh || WarpToNearestNavMesh(agent.transform.position);
        }

        private bool CanReuseCurrentPath(Vector3 destination, float stoppingDistance)
        {
            if (!hasMoveRequest || agent == null || agent.pathPending || !agent.hasPath || agent.isStopped)
            {
                return false;
            }

            var destinationDelta = destination - lastRequestedDestination;
            destinationDelta.y = 0f;
            return destinationDelta.sqrMagnitude <= DestinationReuseDistance * DestinationReuseDistance
                && Mathf.Abs(stoppingDistance - lastRequestedStoppingDistance) <= 0.01f;
        }

        private void ClearMoveRequest()
        {
            hasMoveRequest = false;
            lastRequestedDestination = default;
            lastRequestedStoppingDistance = default;
        }

        private Vector3 GetFacingDirection(Vector3 fallbackDirection)
        {
            if (agent == null)
            {
                return fallbackDirection;
            }

            var direction = agent.steeringTarget - agent.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > VelocityThreshold)
            {
                return direction;
            }

            fallbackDirection.y = 0f;
            return fallbackDirection;
        }

        private void RotateToMovementDirection(Vector3 direction)
        {
            if (agent == null || isFacingLocked || direction.sqrMagnitude <= VelocityThreshold)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            agent.transform.rotation = Quaternion.RotateTowards(
                agent.transform.rotation,
                targetRotation,
                Mathf.Max(0f, agent.angularSpeed) * Time.deltaTime);
        }

        private void UpdateAnimator(Vector3 worldVelocity)
        {
            if (animator == null)
            {
                return;
            }

            if (isEvasionDirectionLocked)
            {
                ApplyLockedEvasionDirection();
                return;
            }

            var planarVelocity = worldVelocity;
            planarVelocity.y = 0f;
            if (planarVelocity.sqrMagnitude <= VelocityThreshold)
            {
                animator.SetFloat(DirectionXParameter, 0f);
                animator.SetFloat(DirectionYParameter, 0f);
                animator.SetBool(IsRunParameter, false);
                return;
            }

            var localVelocity = animator.transform.InverseTransformDirection(planarVelocity.normalized);
            animator.SetFloat(DirectionXParameter, Mathf.Clamp(localVelocity.x, -1f, 1f));
            animator.SetFloat(DirectionYParameter, Mathf.Clamp(localVelocity.z, -1f, 1f));
            animator.SetBool(IsRunParameter, true);
        }

        private void ApplyLockedEvasionDirection()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(DirectionXParameter, evasionDirectionalInput.x);
            animator.SetFloat(DirectionYParameter, evasionDirectionalInput.y);
            animator.SetBool(IsRunParameter, false);
        }
    }
}
