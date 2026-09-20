using Saves;
using UnityEngine;

namespace Advertising
{
    /// <summary>
    /// Project-lifetime state shared by recreated gameplay scopes. It keeps ad cooldowns stable
    /// across location reloads and persists only the new-game grace period required by saves.
    /// </summary>
    public sealed class AdvertisingRuntimeState
    {
        private const float GracePeriodPersistIntervalSeconds = 1f;

        private readonly AdvertisingConfig config;
        private readonly GameSaveController saveController;
        private bool gracePeriodLoaded;
        private bool gracePeriodMustPersist;
        private float newGameGraceRemainingSeconds;
        private float gracePeriodSinceLastPersist;
        private float nextInterstitialAvailableAt;
        private float nextRewardedAvailableAt;

        public AdvertisingRuntimeState(AdvertisingConfig config, GameSaveController saveController)
        {
            this.config = config;
            this.saveController = saveController;
        }

        public bool IsNewGameGracePeriodActive => newGameGraceRemainingSeconds > 0f;

        public void BeginNewGameGracePeriod()
        {
            gracePeriodLoaded = true;
            newGameGraceRemainingSeconds = config.NewGameGracePeriodSeconds;
            gracePeriodSinceLastPersist = 0f;
            gracePeriodMustPersist = true;
            PersistGracePeriodIfPossible();
        }

        public void EnsureGracePeriodLoaded()
        {
            if (gracePeriodLoaded)
            {
                PersistGracePeriodIfPossible();
                return;
            }

            if (!saveController.IsReady)
            {
                return;
            }

            gracePeriodLoaded = true;
            newGameGraceRemainingSeconds = saveController.GetAdvertisingNewGameGraceRemainingSeconds();
        }

        public void AdvanceNewGameGracePeriod(float unscaledDeltaTime)
        {
            if (!gracePeriodLoaded || newGameGraceRemainingSeconds <= 0f)
            {
                return;
            }

            newGameGraceRemainingSeconds = Mathf.Max(0f, newGameGraceRemainingSeconds - Mathf.Max(0f, unscaledDeltaTime));
            gracePeriodSinceLastPersist += Mathf.Max(0f, unscaledDeltaTime);
            gracePeriodMustPersist = true;
            if (newGameGraceRemainingSeconds <= 0f || gracePeriodSinceLastPersist >= GracePeriodPersistIntervalSeconds)
            {
                PersistGracePeriodIfPossible();
            }
        }

        public void PersistGracePeriodIfPossible()
        {
            if (!gracePeriodMustPersist || !saveController.IsReady)
            {
                return;
            }

            saveController.SetAdvertisingNewGameGraceRemainingSeconds(newGameGraceRemainingSeconds);
            gracePeriodMustPersist = false;
            gracePeriodSinceLastPersist = 0f;
        }

        public bool IsInterstitialAvailable(float realtimeSinceStartup) => realtimeSinceStartup >= nextInterstitialAvailableAt;

        public bool IsRewardedAvailable(float realtimeSinceStartup) => realtimeSinceStartup >= nextRewardedAvailableAt;

        public void MarkInterstitialShown(float realtimeSinceStartup)
        {
            nextInterstitialAvailableAt = realtimeSinceStartup + config.InterstitialCooldownSeconds;
        }

        public void MarkRewardedShown(float realtimeSinceStartup)
        {
            nextRewardedAvailableAt = realtimeSinceStartup + config.RewardedCooldownSeconds;
        }
    }
}
