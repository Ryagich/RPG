using System;
using Movement;
using VContainer.Unity;

namespace Inventory
{
    /// <summary>
    /// Adds Wading as a source of the existing weapon-drawing block. The weapon controller
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
            waterDetector.WadingStateChanged += OnWadingStateChanged;
            SetBlocked(waterDetector.IsWading);
        }

        public void Dispose()
        {
            waterDetector.WadingStateChanged -= OnWadingStateChanged;
            SetBlocked(false);
        }

        private void OnWadingStateChanged(bool isWading)
        {
            SetBlocked(isWading);
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
