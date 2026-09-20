using System;
using Container.Game;
using GameModes;
using MessagePipe;
using Messages;
using Saves;
using UnityEngine;
using VContainer.Unity;
using YG;

namespace Advertising
{
    /// <summary>
    /// Coordinates automatic ad placements for one gameplay scene. UI and location systems only
    /// publish their normal lifecycle changes; this service owns all eligibility and SDK calls.
    /// </summary>
    public sealed class AdvertisingService : IStartable, ITickable, IDisposable
    {
        private const string AutomaticRewardedPlacementId = "automatic";
        private const float RequestTimeoutSeconds = 10f;

        private enum AdFormat
        {
            None,
            Interstitial,
            Rewarded
        }

        private readonly AdvertisingConfig config;
        private readonly AdvertisingRuntimeState runtimeState;
        private readonly GameSceneSessionConfiguration sceneSessionConfiguration;
        private readonly GameSaveController saveController;
        private readonly GameWorldBootstrapper worldBootstrapper;
        private readonly GameModesController gameModesController;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        private IDisposable gameModeSubscription;
        private GameMode previousGameMode = GameMode.Game;
        private bool saveReady;
        private AdFormat requestedFormat;
        private bool openedRequestedAd;
        private float requestTimeoutAt;
        private int cursorRestoreFrame = -1;
        private bool pendingLocationPlacement;

        public AdvertisingService(
            AdvertisingConfig config,
            AdvertisingRuntimeState runtimeState,
            GameSceneSessionConfiguration sceneSessionConfiguration,
            GameSaveController saveController,
            GameWorldBootstrapper worldBootstrapper,
            GameModesController gameModesController,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.config = config;
            this.runtimeState = runtimeState;
            this.sceneSessionConfiguration = sceneSessionConfiguration;
            this.saveController = saveController;
            this.worldBootstrapper = worldBootstrapper;
            this.gameModesController = gameModesController;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public async void Start()
        {
            previousGameMode = gameModesController.GameMode;
            gameModeSubscription = gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
            worldBootstrapper.LocationTransitionCompleted += OnLocationTransitionCompleted;
            SubscribeToAdvertisingCallbacks();

            await saveController.Ready;
            saveReady = true;
            runtimeState.EnsureGracePeriodLoaded();
            if (pendingLocationPlacement)
            {
                pendingLocationPlacement = false;
                TryShowAutomaticAd();
            }
        }

        public void Tick()
        {
            if (saveReady && sceneSessionConfiguration.IsGameplayScene)
            {
                runtimeState.AdvanceNewGameGracePeriod(Time.unscaledDeltaTime);
            }

            if (requestedFormat != AdFormat.None && Time.realtimeSinceStartup >= requestTimeoutAt)
            {
                requestedFormat = AdFormat.None;
                openedRequestedAd = false;
            }

            if (cursorRestoreFrame >= 0 && Time.frameCount >= cursorRestoreFrame)
            {
                cursorRestoreFrame = -1;
                gameModesController.RestoreCurrentModeState();
            }
        }

        public void Dispose()
        {
            runtimeState.PersistGracePeriodIfPossible();
            gameModeSubscription?.Dispose();
            worldBootstrapper.LocationTransitionCompleted -= OnLocationTransitionCompleted;
            UnsubscribeFromAdvertisingCallbacks();
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            GameMode currentGameMode = message.GameMode;
            bool returnedToMainGame = currentGameMode == GameMode.Game && IsAutomaticPlacementSource(previousGameMode);
            previousGameMode = currentGameMode;
            if (returnedToMainGame)
            {
                TryShowAutomaticAd();
            }
        }

        private void OnLocationTransitionCompleted()
        {
            if (!saveReady)
            {
                pendingLocationPlacement = true;
                return;
            }

            TryShowAutomaticAd();
        }

        private void TryShowAutomaticAd()
        {
            if (!sceneSessionConfiguration.IsGameplayScene ||
                !saveReady ||
                !config.IsAvailableAt(DateTimeOffset.UtcNow) ||
                runtimeState.IsNewGameGracePeriodActive ||
                requestedFormat != AdFormat.None ||
                YG2.nowAdsShow)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            bool canShowInterstitial = runtimeState.IsInterstitialAvailable(now);
            bool canShowRewarded = runtimeState.IsRewardedAvailable(now);
            if (!canShowInterstitial && !canShowRewarded)
            {
                return;
            }

            requestedFormat = canShowRewarded ? AdFormat.Rewarded : AdFormat.Interstitial;
            openedRequestedAd = false;
            requestTimeoutAt = now + RequestTimeoutSeconds;
            if (requestedFormat == AdFormat.Rewarded)
            {
                YG2.RewardedAdvShow(AutomaticRewardedPlacementId);
            }
            else
            {
                YG2.InterstitialAdvShow();
            }
        }

        private static bool IsAutomaticPlacementSource(GameMode mode)
        {
            return mode is GameMode.Pause or GameMode.PauseSettings or GameMode.Looting or
                GameMode.Dialogue or GameMode.Trade or GameMode.Map or GameMode.Quest or
                GameMode.SwitchLocation;
        }

        private void SubscribeToAdvertisingCallbacks()
        {
            YG2.onOpenInterAdv += OnInterstitialOpened;
            YG2.onCloseInterAdv += OnInterstitialClosed;
            YG2.onCloseInterAdvWasShow += OnInterstitialClosedWithResult;
            YG2.onErrorInterAdv += OnInterstitialError;
            YG2.onOpenRewardedAdv += OnRewardedOpened;
            YG2.onCloseRewardedAdv += OnRewardedClosed;
            YG2.onErrorRewardedAdv += OnRewardedError;
        }

        private void UnsubscribeFromAdvertisingCallbacks()
        {
            YG2.onOpenInterAdv -= OnInterstitialOpened;
            YG2.onCloseInterAdv -= OnInterstitialClosed;
            YG2.onCloseInterAdvWasShow -= OnInterstitialClosedWithResult;
            YG2.onErrorInterAdv -= OnInterstitialError;
            YG2.onOpenRewardedAdv -= OnRewardedOpened;
            YG2.onCloseRewardedAdv -= OnRewardedClosed;
            YG2.onErrorRewardedAdv -= OnRewardedError;
        }

        private void OnInterstitialOpened()
        {
            if (requestedFormat == AdFormat.Interstitial)
            {
                openedRequestedAd = true;
            }
        }

        private void OnInterstitialClosed()
        {
            if (requestedFormat == AdFormat.Interstitial)
            {
                ScheduleCursorRestore();
            }
        }

        private void OnInterstitialClosedWithResult(bool wasShown)
        {
            if (requestedFormat != AdFormat.Interstitial)
            {
                return;
            }

            if (openedRequestedAd && wasShown)
            {
                runtimeState.MarkInterstitialShown(Time.realtimeSinceStartup);
            }

            requestedFormat = AdFormat.None;
            openedRequestedAd = false;
        }

        private void OnInterstitialError()
        {
            if (requestedFormat == AdFormat.Interstitial)
            {
                requestedFormat = AdFormat.None;
                openedRequestedAd = false;
                ScheduleCursorRestore();
            }
        }

        private void OnRewardedOpened()
        {
            if (requestedFormat == AdFormat.Rewarded)
            {
                openedRequestedAd = true;
            }
        }

        private void OnRewardedClosed()
        {
            if (requestedFormat != AdFormat.Rewarded)
            {
                return;
            }

            if (openedRequestedAd)
            {
                runtimeState.MarkRewardedShown(Time.realtimeSinceStartup);
            }

            requestedFormat = AdFormat.None;
            openedRequestedAd = false;
            ScheduleCursorRestore();
        }

        private void OnRewardedError()
        {
            if (requestedFormat == AdFormat.Rewarded)
            {
                requestedFormat = AdFormat.None;
                openedRequestedAd = false;
                ScheduleCursorRestore();
            }
        }

        private void ScheduleCursorRestore()
        {
            cursorRestoreFrame = Mathf.Max(cursorRestoreFrame, Time.frameCount + 1);
        }
    }
}
