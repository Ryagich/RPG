using Inventory;
using Movement;

namespace CharacterAnimation
{
    /// <summary>
    /// Read-only presentation contract for the character's current locomotion and combat facts.
    /// Gameplay systems remain the owners of these facts; animation add-ons consume this single
    /// contract instead of depending on movement, water and weapon systems independently.
    /// </summary>
    public interface ICharacterAnimationState
    {
        bool IsMoving { get; }
        bool IsRunning { get; }
        bool IsSprinting { get; }
        bool IsWading { get; }
        bool IsSwimming { get; }
        bool IsSwimmingRun { get; }
        bool IsSwimmingSprint { get; }
        bool IsAttacking { get; }
    }

    /// <summary>Player-specific projection of gameplay state for presentation features.</summary>
    public sealed class PlayerCharacterAnimationState : ICharacterAnimationState
    {
        private readonly PlayerMovement movement;
        private readonly CharacterWaterDetector waterDetector;
        private readonly PlayerWeaponInHandController weaponController;

        public PlayerCharacterAnimationState(
            PlayerMovement movement,
            CharacterWaterDetector waterDetector,
            PlayerWeaponInHandController weaponController)
        {
            this.movement = movement;
            this.waterDetector = waterDetector;
            this.weaponController = weaponController;
        }

        public bool IsMoving => movement?.IsMoving == true;
        public bool IsRunning => movement?.IsRunning == true;
        public bool IsSprinting => movement?.IsSprinting == true;
        public bool IsWading => waterDetector?.IsWading == true;
        public bool IsSwimming => waterDetector?.IsSwimming == true;
        public bool IsSwimmingRun => movement?.IsSwimmingRun == true;
        public bool IsSwimmingSprint => movement?.IsSwimmingSprint == true;
        public bool IsAttacking => weaponController?.IsAttackAnimationActive == true;
    }
}
