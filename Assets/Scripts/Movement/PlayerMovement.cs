using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CameraScripts;
using Inventory.Inventories;
using Stats;
using TargetLock;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerMovement : ICharacterPlanarMotionSource, IStaminaMovementState
    {
        private const float InputThreshold = 0.001f;

        private readonly CameraMotor cameraMotor;
        private readonly Transform visualTransform;
        private readonly PlayerMovementConfig playerMovementConfig;
        private readonly PlayerInventory playerInventory;
        private readonly StatsController statsController;
        private readonly TargetLockController targetLockController;
        private readonly PlayerMovementIntent intent;
        private readonly PlayerFacingController facingController;

        private Vector2 targetVelocity;
        private Vector2 currentVelocity;
        private readonly HashSet<object> movementLockSources = new();
        private readonly HashSet<object> airborneMomentumSources = new();
        private Vector3 airborneMomentumVelocity;
        private bool isMovementEnabled = true;
        private bool wantsToRun;
        private bool wantsToSprint;
        private bool isRunning;
        private bool isSprinting;
        private bool isRunAllowed = true;
        private float currentAccelerationRate;
        private float currentDecelerationRate;
        private float sprintSpeedBlend;

        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        private PlayerMovement
            (
                PlayerMovementConfig playerMovementConfig,
                CameraMotor cameraMotor,
                Animator animator,
                PlayerInventory playerInventory,
                StatsController statsController,
                TargetLockController targetLockController,
                PlayerMovementIntent intent,
                PlayerFacingController facingController
            )
        {
            this.playerMovementConfig = playerMovementConfig;
            this.cameraMotor = cameraMotor;
            visualTransform = animator.transform;
            this.playerInventory = playerInventory;
            this.statsController = statsController;
            this.targetLockController = targetLockController;
            this.intent = intent;
            this.facingController = facingController;
            currentAccelerationRate = playerMovementConfig.WalkSpeedChangeRate;
            currentDecelerationRate = playerMovementConfig.WalkDecelerationRate;
        }

        public Vector3 GetPlanarVelocity(float deltaTime)
        {
            if (HasAirborneMomentum)
            {
                return airborneMomentumVelocity;
            }

            if (!CanMove)
            {
                currentVelocity = Vector2.zero;
                return Vector3.zero;
            }

            ApplyStaminaRestrictions();
            UpdateSprintSpeedBlend(deltaTime);

            var targetAccelerationRate = isSprinting
                ? playerMovementConfig.SprintSpeedChangeRate
                : isRunning
                    ? playerMovementConfig.RunSpeedChangeRate
                    : playerMovementConfig.WalkSpeedChangeRate;
            currentAccelerationRate = Mathf.MoveTowards(
                currentAccelerationRate,
                targetAccelerationRate,
                playerMovementConfig.SpeedChangeRateBlendSpeed * deltaTime);

            if (targetVelocity.sqrMagnitude > InputThreshold)
            {
                // Remember the movement mode that produced the momentum. Releasing both
                // the movement and run buttons in the same frame must still decelerate as a run.
                currentDecelerationRate = isSprinting
                    ? playerMovementConfig.SprintDecelerationRate
                    : isRunning
                        ? playerMovementConfig.RunDecelerationRate
                        : playerMovementConfig.WalkDecelerationRate;
            }

            var velocityChangeRate = targetVelocity.sqrMagnitude > InputThreshold
                ? currentAccelerationRate
                : currentDecelerationRate;

            currentVelocity = Vector2.MoveTowards(
                currentVelocity,
                targetVelocity,
                velocityChangeRate * deltaTime);

            if (currentVelocity.sqrMagnitude <= InputThreshold)
            {
                return Vector3.zero;
            }

            var worldVelocity = GetWorldVelocity(currentVelocity);
            if (worldVelocity.sqrMagnitude <= InputThreshold)
            {
                return Vector3.zero;
            }

            facingController.Rotate(worldVelocity, intent.Direction, isRunning, isSprinting, deltaTime);
            return worldVelocity;
        }

        public void ChangeState(bool newState)
        {
            isMovementEnabled = newState;
            RefreshMovementState();
        }

        /// <summary>
        /// Applies an independent movement lock without overriding game-mode or other action locks.
        /// Movement input continues to be buffered while at least one source owns a lock.
        /// </summary>
        public void SetMovementLocked(object source, bool isLocked)
        {
            if (source == null)
            {
                return;
            }

            var hadActionMovementLock = movementLockSources.Count > 0;
            if (isLocked)
            {
                movementLockSources.Add(source);
            }
            else
            {
                movementLockSources.Remove(source);
            }

            var restoreBufferedVelocity = hadActionMovementLock
                                          && movementLockSources.Count == 0;
            RefreshMovementState(restoreBufferedVelocity);
        }

        /// <summary>
        /// Preserves the current world-space planar velocity while airborne. This is
        /// intentionally separate from an input lock: the latter blocks control,
        /// whereas this keeps the take-off momentum moving the controller.
        /// </summary>
        public void SetAirborneMomentum(object source, bool isActive, float velocityMultiplier = 1f)
        {
            if (source == null)
            {
                return;
            }

            if (isActive)
            {
                if (airborneMomentumSources.Add(source) && airborneMomentumSources.Count == 1)
                {
                    airborneMomentumVelocity = GetWorldVelocity(currentVelocity) * Mathf.Max(0f, velocityMultiplier);
                }

                return;
            }

            airborneMomentumSources.Remove(source);
            if (!HasAirborneMomentum)
            {
                airborneMomentumVelocity = Vector3.zero;
            }
        }

        public Vector2 CurrentVelocity => currentVelocity;
        /// <summary>
        /// True while an Animation Event owns character movement. Input actions that would
        /// start their own locomotion, such as jumping, must not bypass this lock.
        /// </summary>
        public bool IsMovementLocked => movementLockSources.Count > 0;
        public bool IsRunning => isRunning;
        public bool IsSprinting => isSprinting;
        public bool IsMoving => CanMove && currentVelocity.sqrMagnitude > InputThreshold;

        public void SetRunAllowed(bool isAllowed)
        {
            isRunAllowed = isAllowed;
            ApplyBufferedInputState();
        }

        public void RefreshIntent() => ApplyBufferedInputState();

        private void ApplyBufferedInputState()
        {
            wantsToRun = intent.IsRunPressed || intent.IsSprintPressed;
            wantsToSprint = intent.IsSprintPressed;
            RefreshLocomotionState();
        }

        private void RefreshLocomotionState()
        {
            isRunning = CanMove && wantsToRun && isRunAllowed && CanRunByStamina();
            isSprinting = isRunning
                          && wantsToSprint
                          && HasForwardSprintInput()
                          && !targetLockController.IsLocked;
            targetVelocity = CanMoveByStamina()
                ? isSprinting ? Vector2.up : intent.Direction
                : Vector2.zero;
        }

        private bool CanMove => isMovementEnabled && movementLockSources.Count == 0;
        private bool HasAirborneMomentum => airborneMomentumSources.Count > 0;

        private void RefreshMovementState(bool restoreBufferedVelocity = false)
        {
            if (CanMove)
            {
                ApplyBufferedInputState();
                if (restoreBufferedVelocity)
                {
                    // Root-motion actions own displacement while locked. Their final animation
                    // frame can therefore be followed immediately by input-driven movement
                    // rather than a zero-velocity acceleration frame.
                    currentVelocity = targetVelocity;
                }

                return;
            }

            targetVelocity = Vector2.zero;
            currentVelocity = Vector2.zero;
            wantsToRun = false;
            wantsToSprint = false;
            isRunning = false;
            isSprinting = false;
            sprintSpeedBlend = 0f;
            currentAccelerationRate = playerMovementConfig.WalkSpeedChangeRate;
            currentDecelerationRate = playerMovementConfig.WalkDecelerationRate;
        }

        private void ApplyStaminaRestrictions()
        {
            if (!CanMoveByStamina())
            {
                targetVelocity = Vector2.zero;
                currentVelocity = Vector2.zero;
                isRunning = false;
                isSprinting = false;
                return;
            }

            RefreshLocomotionState();
        }

        private void UpdateSprintSpeedBlend(float deltaTime)
        {
            var targetBlend = isSprinting ? 1f : 0f;
            var changeRate = isSprinting
                ? playerMovementConfig.SprintSpeedChangeRate
                : playerMovementConfig.SprintDecelerationRate;
            sprintSpeedBlend = Mathf.MoveTowards(
                sprintSpeedBlend,
                targetBlend,
                changeRate * deltaTime);
        }

        private bool HasForwardSprintInput()
        {
            return intent.Direction.y > InputThreshold;
        }

        private bool CanMoveByStamina()
        {
            var staminaStat = statsController.GetStat(StatType.Stamina);
            return staminaStat.Value.Value > staminaStat.Min && !playerInventory.IsWeightMovementBlocked();
        }

        private bool CanRunByStamina()
        {
            var staminaStat = (SafeStat)statsController.GetStat(StatType.Stamina);
            if (Mathf.Approximately(staminaStat.Max, 0f))
            {
                return false;
            }

            var normalizedStamina = staminaStat.Value.Value / staminaStat.Max;
            return normalizedStamina > Mathf.Clamp01(staminaStat.MinSafePercent);
        }

        private Vector3 GetWorldVelocity(Vector2 velocity)
        {
            var inputMagnitude = Mathf.Clamp01(velocity.magnitude);
            if (inputMagnitude <= InputThreshold)
            {
                return Vector3.zero;
            }

            if (isSprinting)
            {
                var sprintSpeed = GetSprintAdjustedSpeed(playerMovementConfig.RunForwardSpeed);
                return visualTransform.forward * (sprintSpeed * inputMagnitude * GetWeightSpeedMultiplier());
            }

            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(velocity.x, 0f, velocity.y);
            if (moveDirection.sqrMagnitude <= InputThreshold)
            {
                return Vector3.zero;
            }

            var moveSpeed = CalculateMoveSpeed(moveDirection) * inputMagnitude;
            return moveDirection.normalized * moveSpeed;
        }

        private float CalculateMoveSpeed(Vector3 worldMoveDirection)
        {
            var localMoveDirection = visualTransform.InverseTransformDirection(worldMoveDirection.normalized);
            var hasHorizontalInput = Mathf.Abs(localMoveDirection.x) > InputThreshold;
            var horizontalSpeed = hasHorizontalInput
                ? (isRunning ? playerMovementConfig.RunStrafeSpeed : playerMovementConfig.StrafeSpeed)
                : 0f;
            var verticalSpeed = 0f;

            if (localMoveDirection.z > InputThreshold)
            {
                verticalSpeed = isRunning
                    ? playerMovementConfig.RunForwardSpeed
                    : playerMovementConfig.ForwardSpeed;
            }
            else if (localMoveDirection.z < -InputThreshold)
            {
                verticalSpeed = isRunning
                    ? playerMovementConfig.RunBackwardSpeed
                    : playerMovementConfig.BackwardSpeed;
            }

            if (horizontalSpeed > 0f && verticalSpeed > 0f)
            {
                return GetSprintAdjustedSpeed((horizontalSpeed + verticalSpeed) * 0.5f) * GetWeightSpeedMultiplier();
            }

            var baseSpeed = Mathf.Max(horizontalSpeed, verticalSpeed);
            return GetSprintAdjustedSpeed(baseSpeed) * GetWeightSpeedMultiplier();
        }

        private float GetSprintAdjustedSpeed(float baseSpeed)
        {
            return Mathf.Lerp(baseSpeed, playerMovementConfig.SprintForwardSpeed, sprintSpeedBlend);
        }

        private float GetWeightSpeedMultiplier()
        {
            var weightEffect = playerMovementConfig.EvaluateWeightSpeedPenalty(
                playerInventory.GetMovementSlowdownNormalizedWeight());
            return Mathf.Clamp01(1f - weightEffect * playerMovementConfig.WeightSpeedPenaltyMultiplier);
        }

    }
}
