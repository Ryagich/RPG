using System;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    /// <summary>
    /// Converts water trigger contacts into character immersion and Wading state.
    /// Trigger collection belongs to <see cref="CharacterWaterContactTracker"/>.
    /// </summary>
    public sealed class CharacterWaterDetector : ITickable
    {
        private const float SurfaceEpsilon = 0.001f;

        private readonly CharacterController characterController;
        private readonly CharacterWaterContactTracker waterContactTracker;
        private readonly CharacterWaterConfig waterConfig;

        public bool IsInWater { get; private set; }
        public bool IsWading { get; private set; }
        public float SubmersionHeight { get; private set; }
        public float MovementSpeedMultiplier { get; private set; } = 1f;
        public float MovementSpeedMultiplierChangeRate => waterConfig.MovementSpeedMultiplierChangeRate;
        public event Action<bool> WadingStateChanged;

        private CharacterWaterDetector(
            CharacterController characterController,
            CharacterWaterContactTracker waterContactTracker,
            CharacterWaterConfig waterConfig)
        {
            this.characterController = characterController;
            this.waterContactTracker = waterContactTracker;
            this.waterConfig = waterConfig;
        }

        public void Tick()
        {
            if (characterController == null || !characterController.enabled)
            {
                ClearWaterState();
                return;
            }

            var controllerBounds = characterController.bounds;
            if (!waterContactTracker.TryGetSurfaceHeight(out var waterSurfaceHeight))
            {
                ClearWaterState();
                return;
            }

            IsInWater = true;
            SubmersionHeight = Mathf.Max(0f, waterSurfaceHeight - controllerBounds.min.y);
            var threshold = IsWading
                ? waterConfig.WadingExitSubmersionHeight
                : waterConfig.WadingEnterSubmersionHeight;
            SetWadingState(SubmersionHeight + SurfaceEpsilon >= threshold);
            MovementSpeedMultiplier = waterConfig.EvaluateMovementSpeedMultiplier(SubmersionHeight);
        }

        private void ClearWaterState()
        {
            IsInWater = false;
            SetWadingState(false);
            SubmersionHeight = 0f;
            MovementSpeedMultiplier = 1f;
        }

        private void SetWadingState(bool isWading)
        {
            if (IsWading == isWading)
            {
                return;
            }

            IsWading = isWading;
            WadingStateChanged?.Invoke(isWading);
        }
    }
}
