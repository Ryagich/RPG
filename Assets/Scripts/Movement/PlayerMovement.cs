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
    public class PlayerMovement : ITickable, IStaminaMovementState
    {
        private const float InputThreshold = 0.001f;

        private readonly CameraMotor cameraMotor;
        private readonly Transform playerTransform;
        private readonly Transform visualTransform;
        private readonly CharacterController controller;
        private readonly PlayerMovementConfig playerMovementConfig;
        private readonly PlayerInventory playerInventory;
        private readonly StatsController statsController;
        private readonly TargetLockController targetLockController;

        private Vector2 bufferedInputDirection;
        private Vector2 targetVelocity;
        private Vector2 currentVelocity;
        private readonly HashSet<object> movementLockSources = new();
        private readonly HashSet<object> airborneMomentumSources = new();
        private Vector3 airborneMomentumVelocity;
        private bool isMovementEnabled = true;
        private bool bufferedRunPressed;
        private bool wantsToRun;
        private bool isRunning;
        private bool isRunAllowed = true;
        private float currentAccelerationRate;
        private float currentDecelerationRate;

        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        private PlayerMovement
            (
                PlayerMovementConfig playerMovementConfig,
                CameraMotor cameraMotor,
                Transform playerTransform,
                Animator animator,
                CharacterController controller,
                PlayerInventory playerInventory,
                StatsController statsController,
                TargetLockController targetLockController
            )
        {
            this.playerMovementConfig = playerMovementConfig;
            this.cameraMotor = cameraMotor;
            this.playerTransform = playerTransform;
            visualTransform = animator.transform;
            this.controller = controller;
            this.playerInventory = playerInventory;
            this.statsController = statsController;
            this.targetLockController = targetLockController;
            currentAccelerationRate = playerMovementConfig.WalkSpeedChangeRate;
            currentDecelerationRate = playerMovementConfig.WalkDecelerationRate;
        }

        public void Tick()
        {
            if (controller == null || !controller.enabled)
            {
                return;
            }

            if (HasAirborneMomentum)
            {
                controller.Move(airborneMomentumVelocity * Time.deltaTime);
                return;
            }

            if (!CanMove)
            {
                currentVelocity = Vector2.zero;
                return;
            }

            ApplyStaminaRestrictions();

            var targetAccelerationRate = isRunning
                ? playerMovementConfig.RunSpeedChangeRate
                : playerMovementConfig.WalkSpeedChangeRate;
            currentAccelerationRate = Mathf.MoveTowards(
                currentAccelerationRate,
                targetAccelerationRate,
                playerMovementConfig.SpeedChangeRateBlendSpeed * Time.deltaTime);

            if (targetVelocity.sqrMagnitude > InputThreshold)
            {
                // Remember the movement mode that produced the momentum. Releasing both
                // the movement and run buttons in the same frame must still decelerate as a run.
                currentDecelerationRate = isRunning
                    ? playerMovementConfig.RunDecelerationRate
                    : playerMovementConfig.WalkDecelerationRate;
            }

            var velocityChangeRate = targetVelocity.sqrMagnitude > InputThreshold
                ? currentAccelerationRate
                : currentDecelerationRate;

            currentVelocity = Vector2.MoveTowards(
                currentVelocity,
                targetVelocity,
                velocityChangeRate * Time.deltaTime);

            if (currentVelocity.sqrMagnitude <= InputThreshold)
            {
                return;
            }

            var worldVelocity = GetWorldVelocity(currentVelocity);
            if (worldVelocity.sqrMagnitude <= InputThreshold)
            {
                return;
            }

            RotateTowardsMovement(worldVelocity, currentVelocity);
            controller.Move(worldVelocity * Time.deltaTime);
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

            if (isLocked)
            {
                movementLockSources.Add(source);
            }
            else
            {
                movementLockSources.Remove(source);
            }

            RefreshMovementState();
        }

        /// <summary>
        /// Preserves the current world-space planar velocity while airborne. This is
        /// intentionally separate from an input lock: the latter blocks control,
        /// whereas this keeps the take-off momentum moving the controller.
        /// </summary>
        public void SetAirborneMomentum(object source, bool isActive)
        {
            if (source == null)
            {
                return;
            }

            if (isActive)
            {
                if (airborneMomentumSources.Add(source) && airborneMomentumSources.Count == 1)
                {
                    airborneMomentumVelocity = GetWorldVelocity(currentVelocity);
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
        /// The latest movement input received from the input system. Unlike <see cref="CurrentVelocity"/>,
        /// it remains available when movement is temporarily disabled by an action animation.
        /// </summary>
        public Vector2 CurrentInputDirection => bufferedInputDirection;
        public bool IsRunning => isRunning;
        public bool IsMoving => CanMove && currentVelocity.sqrMagnitude > InputThreshold;

        public void SetRunAllowed(bool isAllowed)
        {
            isRunAllowed = isAllowed;
            isRunning = CanMove && wantsToRun && isRunAllowed && CanRunByStamina();
        }

        public void SetMovementInput(Vector2 direction, bool isRunning)
        {
            bufferedInputDirection = direction;
            bufferedRunPressed = isRunning;
            ApplyBufferedInputState();
        }

        private void ApplyBufferedInputState()
        {
            wantsToRun = bufferedRunPressed;
            targetVelocity = CanMoveByStamina() ? bufferedInputDirection : Vector2.zero;
            isRunning = CanMove && wantsToRun && isRunAllowed && CanRunByStamina();
        }

        private bool CanMove => isMovementEnabled && movementLockSources.Count == 0;
        private bool HasAirborneMomentum => airborneMomentumSources.Count > 0;

        private void RefreshMovementState()
        {
            if (CanMove)
            {
                ApplyBufferedInputState();
                return;
            }

            targetVelocity = Vector2.zero;
            currentVelocity = Vector2.zero;
            wantsToRun = false;
            isRunning = false;
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
                return;
            }

            isRunning = CanMove && wantsToRun && isRunAllowed && CanRunByStamina();
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
            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(velocity.x, 0f, velocity.y);
            if (moveDirection.sqrMagnitude <= InputThreshold)
            {
                return Vector3.zero;
            }

            var inputMagnitude = Mathf.Clamp01(velocity.magnitude);
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
                return ((horizontalSpeed + verticalSpeed) * 0.5f) * GetWeightSpeedMultiplier();
            }

            var baseSpeed = Mathf.Max(horizontalSpeed, verticalSpeed);
            return baseSpeed * GetWeightSpeedMultiplier();
        }

        private float GetWeightSpeedMultiplier()
        {
            var weightEffect = playerMovementConfig.EvaluateWeightSpeedPenalty(
                playerInventory.GetMovementSlowdownNormalizedWeight());
            return Mathf.Clamp01(1f - weightEffect * playerMovementConfig.WeightSpeedPenaltyMultiplier);
        }

        private void RotateTowardsMovement(Vector3 worldMoveDirection, Vector2 movementInput)
        {
            if (targetLockController.IsLocked)
            {
                return;
            }

            // Only forward-driven movement should rotate the character.
            // Strafe and backward input must preserve the current facing so the player can move
            // sideways/backwards relative to the camera instead of turning into the movement vector.
            if (movementInput.y <= InputThreshold)
            {
                return;
            }

            worldMoveDirection.y = 0f;
            if (worldMoveDirection.sqrMagnitude <= InputThreshold)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(worldMoveDirection.normalized, Vector3.up);
            var rotationSpeed = isRunning
                ? playerMovementConfig.RunRotationSpeed
                : playerMovementConfig.WalkRotationSpeed;
            visualTransform.rotation = Quaternion.RotateTowards(
                visualTransform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }
    }
}
