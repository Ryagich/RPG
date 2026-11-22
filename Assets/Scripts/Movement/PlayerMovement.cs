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
    public class PlayerMovement : ICharacterPlanarMotionSource, IStaminaMovementState, System.IDisposable
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
        private readonly CharacterWaterDetector waterDetector;

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
        private bool wasWaterLocomotionActive;

        /// <summary>
        /// Raised after input has been resolved for a water-state change. Consumers can therefore
        /// use the same normal/run/sprint decision on the first animation frame in water.
        /// </summary>
        public event System.Action<CharacterWaterState> WaterLocomotionStateChanged;

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
                PlayerFacingController facingController,
                CharacterWaterDetector waterDetector
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
            this.waterDetector = waterDetector;
            waterDetector.StateChanged += OnWaterStateChanged;
            currentAccelerationRate = playerMovementConfig.WalkSpeedChangeRate;
            currentDecelerationRate = playerMovementConfig.WalkDecelerationRate;
        }

        public void Dispose()
        {
            waterDetector.StateChanged -= OnWaterStateChanged;
        }

        public Vector3 GetPlanarVelocity(float deltaTime)
        {
            RefreshWaterLocomotionState();

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

            var targetAccelerationRate = GetTargetAccelerationRate();
            currentAccelerationRate = Mathf.MoveTowards(
                currentAccelerationRate,
                targetAccelerationRate,
                playerMovementConfig.SpeedChangeRateBlendSpeed * deltaTime);

            if (targetVelocity.sqrMagnitude > InputThreshold)
            {
                // Remember the movement mode that produced the momentum. Releasing both
                // the movement and run buttons in the same frame must still use the
                // deceleration rate of the mode that was actually moving.
                currentDecelerationRate = GetCurrentDecelerationRate();
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

            facingController.Rotate(targetVelocity, intent.Direction, isRunning, isSprinting, deltaTime);
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
        public bool IsSwimmingRun => waterDetector?.IsSwimming == true && isRunning && !isSprinting;
        public bool IsSwimmingSprint => waterDetector?.IsSwimming == true && isSprinting;
        /// <summary>
        /// Raw locomotion intent for the current frame. Unlike <see cref="IsMoving"/>, this
        /// becomes false immediately when the player releases movement, before planar
        /// deceleration finishes.
        /// </summary>
        public bool HasMovementInput => intent.Direction.sqrMagnitude > InputThreshold;
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
            isRunning = CanMove
                        && wantsToRun
                        && isRunAllowed
                        && CanRunByStamina()
                        && HasMovementInputForCurrentLocomotion()
                        && CanRunInCurrentDirection();
            isSprinting = isRunning
                          && wantsToSprint
                          && HasSprintInputInCurrentLocomotion()
                          && CanSprintInCurrentLocomotion()
                          && !targetLockController.IsLocked;
            targetVelocity = CanMoveByStamina()
                ? GetTargetVelocity()
                : Vector2.zero;
        }

        private Vector2 GetTargetVelocity()
        {
            if (isSprinting && !IsSwimmingSprint)
            {
                return Vector2.up;
            }

            return intent.Direction;
        }

        private bool CanMove => isMovementEnabled && movementLockSources.Count == 0;
        private bool HasAirborneMomentum => airborneMomentumSources.Count > 0;

        private bool CanRunInCurrentDirection()
        {
            return !HasBlockedSwimmingRunDirection();
        }

        private bool HasMovementInputForCurrentLocomotion()
        {
            return waterDetector?.IsSwimming != true
                   || intent.Direction.sqrMagnitude > InputThreshold;
        }

        private bool HasBlockedSwimmingRunDirection()
        {
            return waterDetector?.IsSwimming == true
                   && wantsToRun
                   && intent.Direction.y < -InputThreshold;
        }

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

        private float GetTargetAccelerationRate()
        {
            if (waterDetector?.IsSwimming == true)
            {
                if (IsSwimmingSprint)
                {
                    return playerMovementConfig.SwimSprintSpeedChangeRate;
                }

                return IsSwimmingRun
                    ? playerMovementConfig.SwimRunSpeedChangeRate
                    : playerMovementConfig.SwimSpeedChangeRate;
            }

            if (waterDetector?.IsWading == true)
            {
                return playerMovementConfig.WadingSpeedChangeRate;
            }

            if (isSprinting)
            {
                return playerMovementConfig.SprintSpeedChangeRate;
            }

            return isRunning
                ? playerMovementConfig.RunSpeedChangeRate
                : playerMovementConfig.WalkSpeedChangeRate;
        }

        private float GetCurrentDecelerationRate()
        {
            if (waterDetector?.IsSwimming == true)
            {
                if (IsSwimmingSprint)
                {
                    return playerMovementConfig.SwimSprintDecelerationRate;
                }

                return IsSwimmingRun
                    ? playerMovementConfig.SwimRunDecelerationRate
                    : playerMovementConfig.SwimDecelerationRate;
            }

            if (waterDetector?.IsWading == true)
            {
                return playerMovementConfig.WadingDecelerationRate;
            }

            if (isSprinting)
            {
                return playerMovementConfig.SprintDecelerationRate;
            }

            return isRunning
                ? playerMovementConfig.RunDecelerationRate
                : playerMovementConfig.WalkDecelerationRate;
        }

        private void RefreshWaterLocomotionState()
        {
            var isWaterLocomotionActive = waterDetector != null && waterDetector.IsWaterLocomotionActive;
            if (wasWaterLocomotionActive == isWaterLocomotionActive)
            {
                return;
            }

            wasWaterLocomotionActive = isWaterLocomotionActive;
            RefreshLocomotionState();
        }

        private void OnWaterStateChanged(CharacterWaterState state)
        {
            wasWaterLocomotionActive = waterDetector.IsWaterLocomotionActive;
            RefreshLocomotionState();
            WaterLocomotionStateChanged?.Invoke(state);
        }

        private bool HasSprintInputInCurrentLocomotion()
        {
            if (waterDetector?.IsSwimming == true)
            {
                return intent.Direction.sqrMagnitude > InputThreshold
                       && intent.Direction.y >= -InputThreshold;
            }

            return intent.Direction.y > InputThreshold;
        }

        private bool CanSprintInCurrentLocomotion()
        {
            return waterDetector?.IsWaterLocomotionActive != true
                   || waterDetector.IsSwimming;
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

            if (isSprinting && !IsSwimmingSprint)
            {
                var sprintSpeed = GetSprintAdjustedSpeed(playerMovementConfig.RunForwardSpeed);
                var waterAdjustedSprintSpeed = GetWaterAdjustedSpeed(
                    sprintSpeed,
                    playerMovementConfig.WadingForwardSpeed,
                    playerMovementConfig.SwimForwardSpeed);
                return visualTransform.forward * (waterAdjustedSprintSpeed * inputMagnitude * GetWeightSpeedMultiplier());
            }

            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(velocity.x, 0f, velocity.y);
            if (moveDirection.sqrMagnitude <= InputThreshold)
            {
                return Vector3.zero;
            }

            if (TryGetSwimSidewaysForwardSpeed(velocity, out var sidewaysForwardSpeed))
            {
                var cameraPlanarRotation = cameraMotor.GetGameplayPlanarRotation();
                var sidewaysDirection = cameraPlanarRotation * new Vector3(Mathf.Sign(velocity.x), 0f, 0f);
                var forwardDirection = cameraPlanarRotation * Vector3.forward;
                var sidewaysSpeed = GetSwimStrafeSpeed();
                return (sidewaysDirection * sidewaysSpeed + forwardDirection * sidewaysForwardSpeed)
                       * (inputMagnitude * GetWeightSpeedMultiplier());
            }

            var moveSpeed = CalculateMoveSpeed(moveDirection) * inputMagnitude;
            return moveDirection.normalized * moveSpeed;
        }

        private bool TryGetSwimSidewaysForwardSpeed(Vector2 velocity, out float forwardSpeed)
        {
            forwardSpeed = 0f;
            if (waterDetector?.IsSwimming != true
                || !isRunning
                || Mathf.Abs(velocity.x) <= InputThreshold
                || Mathf.Abs(velocity.y) > InputThreshold)
            {
                return false;
            }

            forwardSpeed = IsSwimmingSprint
                ? playerMovementConfig.SwimSprintSidewaysForwardSpeed
                : playerMovementConfig.SwimRunSidewaysForwardSpeed;
            return forwardSpeed > 0f;
        }

        private float CalculateMoveSpeed(Vector3 worldMoveDirection)
        {
            var localMoveDirection = visualTransform.InverseTransformDirection(worldMoveDirection.normalized);
            var hasHorizontalInput = Mathf.Abs(localMoveDirection.x) > InputThreshold;
            var horizontalSpeed = hasHorizontalInput
                ? GetStrafeSpeed()
                : 0f;
            var wadingHorizontalSpeed = hasHorizontalInput
                ? playerMovementConfig.WadingStrafeSpeed
                : 0f;
            var swimHorizontalSpeed = hasHorizontalInput
                ? GetSwimStrafeSpeed()
                : 0f;
            var verticalSpeed = 0f;
            var wadingVerticalSpeed = 0f;
            var swimVerticalSpeed = 0f;

            if (localMoveDirection.z > InputThreshold)
            {
                verticalSpeed = GetForwardSpeed();
                wadingVerticalSpeed = playerMovementConfig.WadingForwardSpeed;
                swimVerticalSpeed = GetSwimForwardSpeed();
            }
            else if (localMoveDirection.z < -InputThreshold)
            {
                verticalSpeed = GetBackwardSpeed();
                wadingVerticalSpeed = playerMovementConfig.WadingBackwardSpeed;
                swimVerticalSpeed = playerMovementConfig.SwimBackwardSpeed;
            }

            if (horizontalSpeed > 0f && verticalSpeed > 0f)
            {
                var landSpeed = GetSprintAdjustedSpeed((horizontalSpeed + verticalSpeed) * 0.5f);
                var wadingSpeed = (wadingHorizontalSpeed + wadingVerticalSpeed) * 0.5f;
                var swimSpeed = (swimHorizontalSpeed + swimVerticalSpeed) * 0.5f;
                return GetWaterAdjustedSpeed(landSpeed, wadingSpeed, swimSpeed) * GetWeightSpeedMultiplier();
            }

            var baseSpeed = Mathf.Max(horizontalSpeed, verticalSpeed);
            var wadingBaseSpeed = Mathf.Max(wadingHorizontalSpeed, wadingVerticalSpeed);
            var swimBaseSpeed = Mathf.Max(swimHorizontalSpeed, swimVerticalSpeed);
            return GetWaterAdjustedSpeed(
                       GetSprintAdjustedSpeed(baseSpeed),
                       wadingBaseSpeed,
                       swimBaseSpeed)
                   * GetWeightSpeedMultiplier();
        }

        private float GetWaterAdjustedSpeed(float landSpeed, float wadingSpeed, float swimSpeed)
        {
            if (waterDetector?.IsSwimming == true)
            {
                return swimSpeed;
            }

            var wadingBlend = waterDetector?.WadingMovementSpeedBlend ?? 0f;
            return Mathf.Lerp(landSpeed, wadingSpeed, wadingBlend);
        }

        private float GetSprintAdjustedSpeed(float baseSpeed)
        {
            return Mathf.Lerp(baseSpeed, playerMovementConfig.SprintForwardSpeed, sprintSpeedBlend);
        }

        private float GetForwardSpeed()
        {
            return isRunning
                ? playerMovementConfig.RunForwardSpeed
                : playerMovementConfig.ForwardSpeed;
        }

        private float GetBackwardSpeed()
        {
            return isRunning
                ? playerMovementConfig.RunBackwardSpeed
                : playerMovementConfig.BackwardSpeed;
        }

        private float GetStrafeSpeed()
        {
            return isRunning
                ? playerMovementConfig.RunStrafeSpeed
                : playerMovementConfig.StrafeSpeed;
        }

        private float GetSwimForwardSpeed()
        {
            if (IsSwimmingSprint)
            {
                return playerMovementConfig.SwimSprintForwardSpeed;
            }

            return IsSwimmingRun
                ? playerMovementConfig.SwimRunForwardSpeed
                : playerMovementConfig.SwimForwardSpeed;
        }

        private float GetSwimStrafeSpeed()
        {
            if (IsSwimmingSprint)
            {
                return playerMovementConfig.SwimSprintStrafeSpeed;
            }

            return IsSwimmingRun
                ? playerMovementConfig.SwimRunStrafeSpeed
                : playerMovementConfig.SwimStrafeSpeed;
        }

        private float GetWeightSpeedMultiplier()
        {
            var weightEffect = playerMovementConfig.EvaluateWeightSpeedPenalty(
                playerInventory.GetMovementSlowdownNormalizedWeight());
            return Mathf.Clamp01(1f - weightEffect * playerMovementConfig.WeightSpeedPenaltyMultiplier);
        }

    }
}
