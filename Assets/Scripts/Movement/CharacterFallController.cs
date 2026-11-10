using Gravity;
using Stats;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    /// <summary>
    /// Classifies one continuous downward fall for animation and landing consequences.
    /// Vertical integration remains in <see cref="PlayerGravity"/>; HP ownership remains in
    /// <see cref="StatsController"/>.
    /// </summary>
    public sealed class CharacterFallController : IStartable, ITickable, System.IDisposable
    {
        private static readonly int FallStageParameterHash = Animator.StringToHash("FallStage");

        private readonly PlayerGravity gravity;
        private readonly FallConfig config;
        private readonly StatsController statsController;
        private readonly Animator animator;

        private bool isFalling;
        private float fallStartedAt;
        private FallStage currentStage;

        public CharacterFallController(
            PlayerGravity gravity,
            FallConfig config,
            StatsController statsController,
            Animator animator)
        {
            this.gravity = gravity;
            this.config = config;
            this.statsController = statsController;
            this.animator = animator;
        }

        public void Start()
        {
            if (gravity == null)
            {
                return;
            }

            gravity.AirborneStateChanged += OnAirborneStateChanged;
            gravity.FallingStateChanged += OnFallingStateChanged;
            SetStage(FallStage.Safe);
        }

        public void Dispose()
        {
            if (gravity == null)
            {
                return;
            }

            gravity.AirborneStateChanged -= OnAirborneStateChanged;
            gravity.FallingStateChanged -= OnFallingStateChanged;
        }

        public void Tick()
        {
            if (!isFalling)
            {
                return;
            }

            SetStage(ResolveStage(Time.time - fallStartedAt));
        }

        private void OnAirborneStateChanged(bool isAirborne)
        {
            if (isAirborne)
            {
                // The previous landing stage must never leak into the next jump while the
                // character is ascending. A new fall begins only after vertical velocity turns down.
                isFalling = false;
                SetStage(FallStage.Safe);
                return;
            }

            if (!isFalling)
            {
                return;
            }

            SetStage(ResolveStage(Time.time - fallStartedAt));
            var landingDamage = GetLandingDamage(currentStage);
            if (landingDamage > 0f)
            {
                // This is environmental damage, not a combat hit: publishing CharacterDamagedMessage
                // here would incorrectly start a weapon-hit reaction and replace the landing animation.
                statsController?.AddValue(StatType.Hp, -landingDamage, StatChangeSource.Environmental);
            }
        }

        private void OnFallingStateChanged(bool startedFalling)
        {
            if (!startedFalling)
            {
                isFalling = false;
                return;
            }

            isFalling = true;
            fallStartedAt = Time.time;
            SetStage(FallStage.Safe);
        }

        private FallStage ResolveStage(float fallDuration)
        {
            if (config == null)
            {
                return FallStage.Safe;
            }

            if (fallDuration >= config.StageThreeStartTime)
            {
                return FallStage.Severe;
            }

            return fallDuration >= config.StageTwoStartTime
                ? FallStage.Hard
                : FallStage.Safe;
        }

        private float GetLandingDamage(FallStage stage)
        {
            if (config == null)
            {
                return 0f;
            }

            return stage switch
            {
                FallStage.Safe => config.StageOneLandingDamage,
                FallStage.Hard => config.StageTwoLandingDamage,
                FallStage.Severe => config.StageThreeLandingDamage,
                _ => 0f
            };
        }

        private void SetStage(FallStage stage)
        {
            if (currentStage == stage && animator != null)
            {
                return;
            }

            currentStage = stage;
            animator?.SetInteger(FallStageParameterHash, (int)stage);
        }
    }

    /// <summary>
    /// Numeric contract for Animator's FallStage parameter: 0 is the existing safe landing,
    /// 1 is a hard landing, and 2 is a severe landing that deals configured environmental damage.
    /// </summary>
    public enum FallStage
    {
        Safe = 0,
        Hard = 1,
        Severe = 2
    }
}
