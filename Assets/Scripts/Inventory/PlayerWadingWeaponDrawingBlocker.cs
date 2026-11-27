using System;
using Movement;
using VContainer.Unity;

namespace Inventory
{
    /// <summary>
    /// Adds water locomotion as a source of the existing weapon-drawing block. The weapon controller
    /// remains the sole owner of sheathing transitions and draw requests.
    /// </summary>
    public sealed class PlayerWadingWeaponDrawingBlocker : IStartable, IDisposable
    {
        private readonly CharacterWaterDetector waterDetector;
        private readonly PlayerWeaponDrawingBlockState weaponDrawingBlockState;

        public PlayerWadingWeaponDrawingBlocker(
            CharacterWaterDetector waterDetector,
            PlayerWeaponDrawingBlockState weaponDrawingBlockState)
        {
            this.waterDetector = waterDetector;
            this.weaponDrawingBlockState = weaponDrawingBlockState;
        }

        public void Start()
        {
            waterDetector.StateChanged += OnWaterStateChanged;
            SetBlocked(waterDetector.IsWaterLocomotionActive);
        }

        public void Dispose()
        {
            waterDetector.StateChanged -= OnWaterStateChanged;
            SetBlocked(false);
        }

        private void OnWaterStateChanged(CharacterWaterState _)
        {
            SetBlocked(waterDetector.IsWaterLocomotionActive);
        }

        private void SetBlocked(bool isBlocked)
        {
            if (isBlocked)
            {
                weaponDrawingBlockState.RegisterSource(this);
                return;
            }

            weaponDrawingBlockState.UnregisterSource(this);
        }
    }
}
