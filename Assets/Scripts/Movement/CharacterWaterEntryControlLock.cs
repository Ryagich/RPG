using System;
using VContainer.Unity;

namespace Movement
{
    /// <summary>
    /// Coordinates the temporary control lock owned by water-entry recovery. The water
    /// constraint decides when recovery begins and ends; movement and presentation keep
    /// ownership of their own lock state.
    /// </summary>
    public sealed class CharacterWaterEntryControlLock : IStartable, IDisposable
    {
        private readonly CharacterWaterSurfaceConstraint waterSurfaceConstraint;
        private readonly PlayerMovement playerMovement;
        private readonly PlayerAnimationController playerAnimationController;

        public CharacterWaterEntryControlLock(
            CharacterWaterSurfaceConstraint waterSurfaceConstraint,
            PlayerMovement playerMovement,
            PlayerAnimationController playerAnimationController)
        {
            this.waterSurfaceConstraint = waterSurfaceConstraint;
            this.playerMovement = playerMovement;
            this.playerAnimationController = playerAnimationController;
        }

        public void Start()
        {
            if (waterSurfaceConstraint == null)
            {
                return;
            }

            waterSurfaceConstraint.EntryControlLockChanged += Apply;
            Apply(waterSurfaceConstraint.IsEntryControlLocked);
        }

        public void Dispose()
        {
            if (waterSurfaceConstraint != null)
            {
                waterSurfaceConstraint.EntryControlLockChanged -= Apply;
            }

            Apply(false);
        }

        private void Apply(bool isLocked)
        {
            playerMovement?.SetMovementLocked(this, isLocked);
            playerAnimationController?.SetLocomotionLocked(this, isLocked);
        }
    }
}
