using System;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    /// <summary>
    /// Converts water trigger contacts into immersion facts and the character's water state.
    /// Trigger collection belongs to <see cref="CharacterWaterContactTracker"/>.
    /// </summary>
    public sealed class CharacterWaterDetector : ITickable, ICharacterAirborneStateBlocker
    {
        private const float SurfaceEpsilon = 0.001f;

        private readonly CharacterController characterController;
        private readonly CharacterWaterContactTracker waterContactTracker;
        private readonly CharacterWaterBottomProbe waterBottomProbe;
        private readonly CharacterWaterConfig waterConfig;

        public bool IsInWater { get; private set; }
        public CharacterWaterState State { get; private set; }
        public bool IsWading => State == CharacterWaterState.Wading;
        public bool IsSwimming => State == CharacterWaterState.Swim;
        public bool IsWaterLocomotionActive => State is CharacterWaterState.Wading or CharacterWaterState.Swim;
        public bool BlocksAirborneState => IsSwimming;
        public float SubmersionHeight { get; private set; }
        public float SurfaceHeight { get; private set; }
        public float BottomHeight { get; private set; }
        public float WaterDepth { get; private set; }
        public float WadingMovementSpeedBlend { get; private set; }
        public event Action<bool> WadingStateChanged;
        public event Action<CharacterWaterState> StateChanged;
        event Action ICharacterAirborneStateBlocker.StateChanged
        {
            add => airborneStateChanged += value;
            remove => airborneStateChanged -= value;
        }
        private event Action airborneStateChanged;

        private CharacterWaterDetector(
            CharacterController characterController,
            CharacterWaterContactTracker waterContactTracker,
            CharacterWaterBottomProbe waterBottomProbe,
            CharacterWaterConfig waterConfig)
        {
            this.characterController = characterController;
            this.waterContactTracker = waterContactTracker;
            this.waterBottomProbe = waterBottomProbe;
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
            if (!waterContactTracker.TryGetWaterVolume(out var waterVolume))
            {
                ClearWaterState();
                return;
            }

            IsInWater = true;
            SurfaceHeight = waterVolume.SurfaceHeight;
            BottomHeight = waterVolume.BottomHeight;
            WaterDepth = waterVolume.Depth;
            SubmersionHeight = Mathf.Max(0f, SurfaceHeight - controllerBounds.min.y);
            var nextState = ResolveState(waterBottomProbe.HasStandableBottom(SurfaceHeight));
            WadingMovementSpeedBlend = ResolveWadingMovementSpeedBlend(nextState);
            SetState(nextState);
        }

        private void ClearWaterState()
        {
            IsInWater = false;
            SubmersionHeight = 0f;
            SurfaceHeight = 0f;
            BottomHeight = 0f;
            WaterDepth = 0f;
            WadingMovementSpeedBlend = 0f;
            SetState(CharacterWaterState.Dry);
        }

        private CharacterWaterState ResolveState(bool hasStandableBottom)
        {
            // A physical bottom inside the shallow-water probe range takes precedence over
            // trigger volume depth. It lets a character stand at the whole wading range and
            // prevents alternating Swim and Wading states at the boundary.
            if (hasStandableBottom)
            {
                var bottomWadingThreshold = IsWading
                    ? waterConfig.WadingExitSubmersionHeight
                    : waterConfig.WadingEnterSubmersionHeight;
                return SubmersionHeight + SurfaceEpsilon >= bottomWadingThreshold
                    ? CharacterWaterState.Wading
                    : CharacterWaterState.Dry;
            }

            // On the first frame in deep water the controller's bottom can still be at the
            // surface, so submersion is near zero. Without a nearby physical bottom, configured
            // deep water enters Swim immediately.
            if (WaterDepth + SurfaceEpsilon >= waterConfig.DeepWaterEntryMinimumDepth)
            {
                return CharacterWaterState.Swim;
            }

            if (IsSwimming
                && SubmersionHeight + SurfaceEpsilon >= waterConfig.SwimExitSubmersionHeight)
            {
                return CharacterWaterState.Swim;
            }

            if (SubmersionHeight + SurfaceEpsilon >= waterConfig.SwimEnterSubmersionHeight)
            {
                return CharacterWaterState.Swim;
            }

            var wadingThreshold = IsWading
                ? waterConfig.WadingExitSubmersionHeight
                : waterConfig.WadingEnterSubmersionHeight;
            return SubmersionHeight + SurfaceEpsilon >= wadingThreshold
                ? CharacterWaterState.Wading
                : CharacterWaterState.Dry;
        }

        private float ResolveWadingMovementSpeedBlend(CharacterWaterState nextState)
        {
            if (nextState == CharacterWaterState.Wading)
            {
                return 1f;
            }

            return nextState == CharacterWaterState.Dry
                ? waterConfig.EvaluateWadingMovementSpeedBlend(SubmersionHeight)
                : 0f;
        }

        private void SetState(CharacterWaterState state)
        {
            if (State == state)
            {
                return;
            }

            var wasWading = IsWading;
            State = state;
            StateChanged?.Invoke(state);
            airborneStateChanged?.Invoke();
            if (wasWading != IsWading)
            {
                WadingStateChanged?.Invoke(IsWading);
            }
        }
    }

    /// <summary>
    /// Character locomotion states caused by water. Underwater is intentionally reserved for
    /// the later diving mechanic; it must not be inferred from the surface state.
    /// </summary>
    public enum CharacterWaterState
    {
        Dry = 0,
        Wading = 1,
        Swim = 2,
        Underwater = 3
    }
}
