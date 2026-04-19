using System.Diagnostics.CodeAnalysis;
using Inventory.Inventories;
using MessagePipe;
using Messages;
using Stats;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerMovement : ITickable
    {
        private const float InputThreshold = 0.001f;

        private readonly Camera cam;
        private readonly Transform playerTransform;
        private readonly Transform visualTransform;
        private readonly CharacterController controller;
        private readonly PlayerMovementConfig playerMovementConfig;
        private readonly PlayerInventory playerInventory;
        private readonly StatsController statsController;

        private Vector2 targetVelocity;
        private Vector2 currentVelocity;
        private bool canMove = true;
        private bool wantsToRun;
        private bool isRunning;
        private float currentSpeedChangeRate;

        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        private PlayerMovement
            (
                PlayerMovementConfig playerMovementConfig,
                Camera cam,
                Transform playerTransform,
                Animator animator,
                CharacterController controller,
                PlayerInventory playerInventory,
                StatsController statsController,
                ISubscriber<PlayerMoveMessage> playerMoveSubscriber
            )
        {
            this.playerMovementConfig = playerMovementConfig;
            this.cam = cam;
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

            RotateTowardsCursor();
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

            var moveDirection = Quaternion.Euler(0, cam.transform.rotation.eulerAngles.y, 0) *
                                new Vector3(currentVelocity.x, 0, currentVelocity.y);
            var inputMagnitude = Mathf.Clamp01(currentVelocity.magnitude);
            var moveSpeed = CalculateMoveSpeed(moveDirection) * inputMagnitude;

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
            }
        }

        public Vector2 CurrentVelocity => currentVelocity;
        public bool IsRunning => isRunning;
        public bool IsMoving => canMove && currentVelocity.sqrMagnitude > InputThreshold;

        private void OnMove(PlayerMoveMessage msg)
        {
            wantsToRun = msg.IsRunning;
            targetVelocity = CanMoveByStamina() ? msg.Direction : Vector2.zero;
            isRunning = wantsToRun && CanRunByStamina();
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

            isRunning = wantsToRun && CanRunByStamina();
        }

        private bool CanMoveByStamina()
        {
            var staminaStat = statsController.GetStat(StatType.Stamina);
            return staminaStat.Value.Value > staminaStat.Min && !playerInventory.IsWeightMovementBlocked();
        }

        private bool CanRunByStamina()
        {
            var staminaStat = statsController.GetStat(StatType.Stamina);
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

        private void RotateTowardsCursor()
        {
            var pointer = Pointer.current;

            if (pointer == null)
            {
                return;
            }

            var ray = cam.ScreenPointToRay(pointer.position.ReadValue());
            var groundPlane = new Plane(Vector3.up, playerTransform.position);

            if (!groundPlane.Raycast(ray, out var hitDistance))
            {
                return;
            }

            var lookPoint = ray.GetPoint(hitDistance);
            var lookDirection = lookPoint - playerTransform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude <= InputThreshold)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
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
