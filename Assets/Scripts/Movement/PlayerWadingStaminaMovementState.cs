using Stats;

namespace Movement
{
    /// <summary>
    /// Keeps generic movement drain semantics for the player, while excluding Wading from
    /// running and sprinting modifiers. Wading owns its dedicated drain separately.
    /// </summary>
    public sealed class PlayerWadingStaminaMovementState : IStaminaMovementState, IStaminaMovementDrainBlockState
    {
        private readonly PlayerMovement playerMovement;
        private readonly CharacterWaterDetector waterDetector;

        public PlayerWadingStaminaMovementState(
            PlayerMovement playerMovement,
            CharacterWaterDetector waterDetector)
        {
            this.playerMovement = playerMovement;
            this.waterDetector = waterDetector;
        }

        public bool IsMoving => playerMovement.IsMoving;
        public bool IsRunning => !waterDetector.IsWading && playerMovement.IsRunning;
        public bool IsSprinting => !waterDetector.IsWading && playerMovement.IsSprinting;
        public bool IsMovementDrainBlocked => waterDetector.IsWading;
    }
}
