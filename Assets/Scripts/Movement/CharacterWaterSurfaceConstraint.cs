using UnityEngine;

namespace Movement
{
    /// <summary>
    /// Keeps a character at a configured depth below a detected water surface once the water
    /// is deep enough to submerge that point. This deliberately takes precedence over a deep
    /// bottom contact, while shallow water continues to use ordinary grounded movement.
    /// It supplies only a vertical-velocity constraint; PlayerGravity still owns the velocity
    /// state and CharacterMotor remains the only caller of CharacterController.Move.
    /// </summary>
    public sealed class CharacterWaterSurfaceConstraint : ICharacterVerticalMotionConstraint
    {
        private const float SurfaceHoldEpsilon = 0.01f;

        private readonly CharacterController controller;
        private readonly CharacterWaterDetector waterDetector;
        private readonly CharacterWaterConfig waterConfig;
        private readonly PlayerMovement playerMovement;
        private bool wasSwimming;
        private bool isBuoyancyEntryActive;
        private bool hasReachedEntrySubmersionDepth;
        private bool isEntryControlLocked;
        private bool hasSurfaceHoldTarget;
        private float currentSurfaceHoldSubmersionHeight;

        public event System.Action<bool> EntryControlLockChanged;
        public bool IsEntryControlLocked => isEntryControlLocked;

        public CharacterWaterSurfaceConstraint(
            CharacterController controller,
            CharacterWaterDetector waterDetector,
            CharacterWaterConfig waterConfig,
            PlayerMovement playerMovement)
        {
            this.controller = controller;
            this.waterDetector = waterDetector;
            this.waterConfig = waterConfig;
            this.playerMovement = playerMovement;
        }

        public bool TryGetVerticalVelocity(
            float deltaTime,
            bool wasGrounded,
            float currentVerticalVelocity,
            out float verticalVelocity)
        {
            if (!IsSwimHoldActive())
            {
                wasSwimming = false;
                isBuoyancyEntryActive = false;
                hasReachedEntrySubmersionDepth = false;
                hasSurfaceHoldTarget = false;
                SetEntryControlLocked(false);
                verticalVelocity = 0f;
                return false;
            }

            BeginEntryBuoyancy(wasGrounded, currentVerticalVelocity);
            if (TryApplyEntryBuoyancy(deltaTime, currentVerticalVelocity, out verticalVelocity))
            {
                return true;
            }

            var targetSurfaceHoldSubmersionHeight = GetSurfaceHoldSubmersionHeight();
            if (!hasSurfaceHoldTarget)
            {
                currentSurfaceHoldSubmersionHeight = targetSurfaceHoldSubmersionHeight;
                hasSurfaceHoldTarget = true;
            }
            else
            {
                // Locomotion mode is state, not input. When normal, run, or sprint swimming
                // selects another configured depth, the controller must move even if the
                // input that caused that transition has already been released this frame.
                currentSurfaceHoldSubmersionHeight = Mathf.MoveTowards(
                    currentSurfaceHoldSubmersionHeight,
                    targetSurfaceHoldSubmersionHeight,
                    waterConfig.SwimSurfaceHoldTransitionSpeed * Mathf.Max(deltaTime, 0f));
            }

            var targetBottomHeight = waterDetector.SurfaceHeight - currentSurfaceHoldSubmersionHeight;
            var heightError = targetBottomHeight - controller.bounds.min.y;
            if (Mathf.Abs(heightError) <= SurfaceHoldEpsilon)
            {
                verticalVelocity = 0f;
                return true;
            }

            var safeDeltaTime = Mathf.Max(deltaTime, float.Epsilon);
            var correctionVelocity = heightError / safeDeltaTime;
            verticalVelocity = Mathf.Clamp(
                correctionVelocity,
                -waterConfig.SwimSurfaceHoldMaxVerticalSpeed,
                waterConfig.SwimSurfaceHoldMaxVerticalSpeed);
            return true;
        }

        private void BeginEntryBuoyancy(bool wasGrounded, float currentVerticalVelocity)
        {
            if (wasSwimming)
            {
                return;
            }

            wasSwimming = true;
            // The small grounded stick velocity is not falling momentum. Only a real airborne
            // water entry is allowed to carry its downward speed into the water.
            isBuoyancyEntryActive = !wasGrounded && currentVerticalVelocity < -SurfaceHoldEpsilon;
            hasReachedEntrySubmersionDepth = false;
            SetEntryControlLocked(isBuoyancyEntryActive);
        }

        private bool TryApplyEntryBuoyancy(
            float deltaTime,
            float currentVerticalVelocity,
            out float verticalVelocity)
        {
            if (!isBuoyancyEntryActive)
            {
                verticalVelocity = 0f;
                return false;
            }

            var targetBottomHeight = waterDetector.SurfaceHeight - GetSurfaceHoldSubmersionHeight();
            var heightError = targetBottomHeight - controller.bounds.min.y;
            // The target depth is a surface-hold equilibrium, not an entry barrier. The
            // incoming downward velocity must first be allowed to carry the character under
            // the surface. Only after reaching that depth may we stop the return ascent there.
            if (!hasReachedEntrySubmersionDepth
                && controller.bounds.min.y <= targetBottomHeight + SurfaceHoldEpsilon)
            {
                hasReachedEntrySubmersionDepth = true;
            }

            verticalVelocity = Mathf.MoveTowards(
                currentVerticalVelocity,
                waterConfig.SwimBuoyancyMaxAscentSpeed,
                waterConfig.SwimBuoyancyAcceleration * Mathf.Max(deltaTime, 0f));
            if (hasReachedEntrySubmersionDepth
                && verticalVelocity >= 0f
                && heightError <= SurfaceHoldEpsilon)
            {
                verticalVelocity = 0f;
                isBuoyancyEntryActive = false;
                SetEntryControlLocked(false);
                return true;
            }

            if (hasReachedEntrySubmersionDepth && verticalVelocity > 0f)
            {
                var safeDeltaTime = Mathf.Max(deltaTime, float.Epsilon);
                verticalVelocity = Mathf.Min(verticalVelocity, heightError / safeDeltaTime);
            }
            if (verticalVelocity >= -waterConfig.SwimControlRestoreMaxDownwardSpeed)
            {
                SetEntryControlLocked(false);
            }

            return true;
        }

        private void SetEntryControlLocked(bool value)
        {
            if (isEntryControlLocked == value)
            {
                return;
            }

            isEntryControlLocked = value;
            EntryControlLockChanged?.Invoke(value);
        }

        private bool IsSwimHoldActive()
        {
            return controller != null
                   && controller.enabled
                   && controller.gameObject.activeInHierarchy
                   && waterDetector != null
                   && waterDetector.IsSwimming
                   && waterConfig != null
                   && waterDetector.IsInWater;
        }

        private float GetSurfaceHoldSubmersionHeight()
        {
            if (playerMovement is { IsSprinting: true })
            {
                return waterConfig.SwimSprintSurfaceHoldSubmersionHeight;
            }

            return playerMovement is { IsRunning: true }
                ? waterConfig.SwimRunSurfaceHoldSubmersionHeight
                : waterConfig.SwimSurfaceHoldSubmersionHeight;
        }
    }
}
