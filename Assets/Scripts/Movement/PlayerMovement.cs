using System.Diagnostics.CodeAnalysis;
using CameraScripts;
using Inventory.Inventories;
using MessagePipe;
using Messages;
using Stats;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerMovement : ITickable
    {
        private const float InputThreshold = 0.001f;

        private readonly CameraMotor cameraMotor;
        private readonly Transform playerTransform;
        private readonly Transform visualTransform;
        private readonly CharacterController controller;
        private readonly PlayerMovementConfig playerMovementConfig;
        private readonly PlayerInventory playerInventory;
        private readonly StatsController statsController;

        private Vector2 bufferedInputDirection;
        private Vector2 targetVelocity;
        private Vector2 currentVelocity;
        private bool canMove = true;
        private bool bufferedRunPressed;
        private bool wantsToRun;
        private bool isRunning;
        private bool isRunAllowed = true;
        private float currentSpeedChangeRate;

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
                ISubscriber<PlayerMoveMessage> playerMoveSubscriber
            )
        {
            this.playerMovementConfig = playerMovementConfig;
            this.cameraMotor = cameraMotor;
            this.playerTransform = playerTransform;
            visualTransform = animator.transform;
            this.controller = controller;
            this.playerInventory = playerInventory;
            this.statsController = statsController;
            currentSpeedChangeRate = playerMovementConfig.WalkSpeedChangeRate;

            playerMoveSubscriber.Subscribe(OnMove);
        }

        public void Tick()
        {
            if (!canMove)
            {
                currentVelocity = Vector2.zero;
                return;
            }

            ApplyStaminaRestrictions();

            var targetSpeedChangeRate = isRunning
                ? playerMovementConfig.RunSpeedChangeRate
                : playerMovementConfig.WalkSpeedChangeRate;
            currentSpeedChangeRate = Mathf.MoveTowards(
                currentSpeedChangeRate,
                targetSpeedChangeRate,
                playerMovementConfig.SpeedChangeRateBlendSpeed * Time.deltaTime);

            currentVelocity = Vector2.MoveTowards(
                currentVelocity,
                targetVelocity,
                currentSpeedChangeRate * Time.deltaTime);

            if (currentVelocity.sqrMagnitude <= InputThreshold)
            {
                return;
            }

            var moveDirection = cameraMotor.GetGameplayPlanarRotation() *
                                new Vector3(currentVelocity.x, 0, currentVelocity.y);
            var inputMagnitude = Mathf.Clamp01(currentVelocity.magnitude);
            var moveSpeed = CalculateMoveSpeed(moveDirection) * inputMagnitude;

            RotateTowardsMovement(moveDirection, currentVelocity);
            controller.Move(moveDirection.normalized * (moveSpeed * Time.deltaTime));
        }

        public void ChangeState(bool newState)
        {
            canMove = newState;

            if (!newState)
            {
                targetVelocity = Vector2.zero;
                currentVelocity = Vector2.zero;
                wantsToRun = false;
                isRunning = false;
                currentSpeedChangeRate = playerMovementConfig.WalkSpeedChangeRate;
                return;
            }

            ApplyBufferedInputState();
        }

        public Vector2 CurrentVelocity => currentVelocity;
        public bool IsRunning => isRunning;
        public bool IsMoving => canMove && currentVelocity.sqrMagnitude > InputThreshold;

        public void SetRunAllowed(bool isAllowed)
        {
            isRunAllowed = isAllowed;
            isRunning = canMove && wantsToRun && isRunAllowed && CanRunByStamina();
        }

        private void OnMove(PlayerMoveMessage msg)
        {
            bufferedInputDirection = msg.Direction;
            bufferedRunPressed = msg.IsRunning;
            ApplyBufferedInputState();
        }

        private void ApplyBufferedInputState()
        {
            wantsToRun = bufferedRunPressed;
            targetVelocity = CanMoveByStamina() ? bufferedInputDirection : Vector2.zero;
            isRunning = canMove && wantsToRun && isRunAllowed && CanRunByStamina();
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

            isRunning = wantsToRun && isRunAllowed && CanRunByStamina();
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
            var staminaStat = (Stamina)statsController.GetStat(StatType.Stamina);
            var weightEffect = staminaStat.EvaluateWeightDrainMultiplier(playerInventory.GetMovementSlowdownNormalizedWeight(), 1f);
            return Mathf.Clamp01(1f - weightEffect * playerMovementConfig.WeightSpeedPenaltyMultiplier);
        }

        private void RotateTowardsMovement(Vector3 worldMoveDirection, Vector2 movementInput)
        {
            // Pure strafe should keep the current facing.
            // This prevents A/D from turning the character and creating an unwanted camera orbit feel.
            if (Mathf.Abs(movementInput.y) <= InputThreshold && Mathf.Abs(movementInput.x) > InputThreshold)
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
