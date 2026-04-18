using System.Collections.Generic;
using Interactable;
using MessagePipe;
using Messages;
using Stats;
using UI.Configs;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Utils;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class MainPage : BasePage
    {
        private static readonly StatType[] AdditionalStatTypes = { StatType.Water, StatType.Food, StatType.Chill };
        private static readonly StatType[] AllStatTypes = { StatType.Hp, StatType.Water, StatType.Food, StatType.Chill };

        private enum HpFillMode
        {
            Synced,
            FillAnimated,
            ChangedFillAnimated
        }

        private enum VisibilityPhase
        {
            Hidden,
            Restoring,
            Showing,
            Fading,
            Holding
        }

        private sealed class StatVisibilityState
        {
            public bool IsCritical;
            public bool KeepIconPinnedUntilFade;
            public float Alpha;
            public float StartAlpha;
            public float TargetAlpha;
            public float PhaseDuration;
            public float PhaseElapsed;
            public VisibilityPhase Phase;
        }

        private readonly UIConfig uiConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly StatFillers statFillers;
        private readonly StatFiller hpFiller;
        private readonly PlayerInteractableLogic playerInteractableLogic;
        private readonly ItemHolderInteractableLogic itemHolderInteractableLogic;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly CompositeDisposable drawDisposables = new();
        private readonly Dictionary<StatType, StatVisibilityState> statVisibilityStates = new();

        public override PageType Type { get; } = PageType.MainGame;

        private RectTransform contentRect;
        private StatsHolder statsHolder;
        private InteractableInterface interactableInterface;
        private BeatingHeart beatingHeart;
        private float lastHpTarget;
        private HpFillMode hpFillMode;
        private float globalAlpha;
        private float globalStartAlpha;
        private float globalTargetAlpha;
        private float globalPhaseDuration;
        private float globalPhaseElapsed;
        private VisibilityPhase globalPhase;
        private bool holdGlobalAtFull;

        public MainPage
            (
                UIConfig uiConfig,
                StatsConfig statsConfig,
                StatsController statsController,
                StatFillers statFillers,
                Canvas canvas,
                PlayerInteractableLogic playerInteractableLogic,
                ItemHolderInteractableLogic itemHolderInteractableLogic,
                IObjectResolver resolver,
                ISubscriber<ShowStatsInputMessage> showStatsInputSubscriber
            )
        {
            this.resolver = resolver;
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.statFillers = statFillers;
            hpFiller = statFillers.Get(StatType.Hp);
            this.playerInteractableLogic = playerInteractableLogic;
            this.itemHolderInteractableLogic = itemHolderInteractableLogic;

            canvasRect = canvas.GetComponent<RectTransform>();

            showStatsInputSubscriber.Subscribe(OnShowStatsInputChanged);
        }

        public override void Draw()
        {
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            statsHolder = resolver.Instantiate(uiConfig.StatsHolder, contentRect);
            statsHolder.name = $"{uiConfig.StatsHolder.name} | {Type}";

            interactableInterface = new InteractableInterface
                (
                    uiConfig,
                    contentRect,
                    playerInteractableLogic,
                    itemHolderInteractableLogic
                );

            InitializeVisibilityState();

            lastHpTarget = statsController.Hp.Value.Value;
            hpFillMode = HpFillMode.Synced;

            hpFiller.Current
                    .Subscribe(_ => RefreshHpFill())
                    .AddTo(drawDisposables);
            statsController.Changed
                           .Subscribe(OnStatChanged)
                           .AddTo(drawDisposables);
            statsController.Hp.Value
                           .Subscribe(newTarget =>
                                      {
                                          OnHpTargetChanged(newTarget);
                                          UpdateCriticalState(StatType.Hp);
                                      })
                           .AddTo(drawDisposables);

            foreach (var statType in AdditionalStatTypes)
            {
                var currentStatType = statType;
                var filler = statFillers.Get(currentStatType);
                filler.Current
                      .Subscribe(_ => RefreshStatFill(currentStatType))
                      .AddTo(drawDisposables);
                statsController.GetStat(currentStatType).Value
                               .Subscribe(_ =>
                                          {
                                              RefreshStatFill(currentStatType);
                                              UpdateCriticalState(currentStatType);
                                          })
                               .AddTo(drawDisposables);
            }

            Observable.EveryUpdate()
                      .Subscribe(_ => TickVisibility())
                      .AddTo(drawDisposables);

            RefreshHpFill();
            RefreshAdditionalStatFills();

            foreach (var statType in AllStatTypes)
            {
                UpdateCriticalState(statType);
            }

            BeginGlobalReleaseSequence();
            ApplyAllVisualAlphas();

            beatingHeart = new BeatingHeart(statsConfig, statsController.Hp, hpFiller, statsHolder.HPHolder);
        }

        public override void Hide()
        {
            beatingHeart?.Dispose();
            beatingHeart = null;

            drawDisposables.Clear();
            statVisibilityStates.Clear();

            interactableInterface?.Dispose();
            interactableInterface = null;

            statsHolder = null;

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void RefreshHpFill()
        {
            var hpHolder = statsHolder?.HPHolder;
            if (hpHolder == null || hpHolder.Fill == null || hpHolder.ChangedFill == null)
            {
                return;
            }

            var normalizedCurrent = GetNormalizedHp(hpFiller.Current.Value);
            var normalizedTarget = GetNormalizedHp(statsController.Hp.Value.Value);

            switch (ResolveHpFillDirection())
            {
                case HpFillMode.FillAnimated:
                    hpHolder.Fill.fillAmount = normalizedCurrent;
                    hpHolder.ChangedFill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.color = statsConfig.HpRecoveryColor;
                    break;
                case HpFillMode.ChangedFillAnimated:
                    hpHolder.Fill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.fillAmount = normalizedCurrent;
                    hpHolder.ChangedFill.color = statsConfig.HpDecreaseColor;
                    break;
                default:
                    hpHolder.Fill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.color = statsConfig.HpFullColor;
                    break;
            }

            ApplyCriticalColor(hpHolder, statsController.Hp, normalizedTarget);
            ApplyVisualAlpha(StatType.Hp);
        }

        private void RefreshAdditionalStatFills()
        {
            foreach (var statType in AdditionalStatTypes)
            {
                RefreshStatFill(statType);
            }
        }

        private void RefreshStatFill(StatType statType)
        {
            var statHolder = statsHolder?.GetHolder(statType);
            if (statHolder == null || statHolder.Fill == null || statHolder.ChangedFill == null)
            {
                return;
            }

            var stat = statsController.GetStat(statType);
            var filler = statFillers.Get(statType);
            var normalizedCurrent = GetNormalizedStat(stat, filler.Current.Value);
            var normalizedTarget = GetNormalizedStat(stat, stat.Value.Value);

            if (normalizedTarget > normalizedCurrent)
            {
                statHolder.Fill.fillAmount = normalizedCurrent;
                statHolder.ChangedFill.fillAmount = normalizedTarget;
                statHolder.ChangedFill.color = statsConfig.HpRecoveryColor;
                ApplyCriticalColor(statHolder, stat, normalizedTarget);
                ApplyVisualAlpha(statType);
                return;
            }

            if (normalizedTarget < normalizedCurrent)
            {
                statHolder.Fill.fillAmount = normalizedTarget;
                statHolder.ChangedFill.fillAmount = normalizedCurrent;
                statHolder.ChangedFill.color = statsConfig.HpDecreaseColor;
                ApplyCriticalColor(statHolder, stat, normalizedTarget);
                ApplyVisualAlpha(statType);
                return;
            }

            statHolder.Fill.fillAmount = normalizedTarget;
            statHolder.ChangedFill.fillAmount = normalizedTarget;
            statHolder.ChangedFill.color = statsConfig.HpFullColor;
            ApplyCriticalColor(statHolder, stat, normalizedTarget);
            ApplyVisualAlpha(statType);
        }

        private float GetNormalizedHp(float value)
        {
            var maxHp = statsController.Hp.Max;
            return Mathf.Approximately(maxHp, 0f) ? 0f : value / maxHp;
        }

        private static float GetNormalizedStat(Stat stat, float value)
        {
            return Mathf.Approximately(stat.Max, 0f) ? 0f : value / stat.Max;
        }

        private void ApplyCriticalColor(StatHolder statHolder, Stat stat, float normalizedTarget)
        {
            var safeThreshold = Mathf.Clamp01(stat.MinSafePercent);
            var fillColor = normalizedTarget >= safeThreshold
                ? statsConfig.HpFullColor
                : Color.Lerp(statsConfig.HpDecreaseColor, statsConfig.HpFullColor, safeThreshold <= 0f ? 0f : normalizedTarget / safeThreshold);

            statHolder.Fill.color = fillColor;

            if (statHolder.Icon != null)
            {
                statHolder.Icon.color = fillColor;
            }
        }

        private void OnHpTargetChanged(float newTarget)
        {
            SelectHpFillMode(newTarget);
            lastHpTarget = newTarget;
            RefreshHpFill();
        }

        private HpFillMode ResolveHpFillDirection()
        {
            if (!Mathf.Approximately(hpFiller.Current.Value, statsController.Hp.Value.Value))
            {
                return hpFillMode;
            }

            return HpFillMode.Synced;
        }

        private void SelectHpFillMode(float newTarget)
        {
            var hpHolder = statsHolder?.HPHolder;
            if (hpHolder == null || hpHolder.Fill == null || hpHolder.ChangedFill == null)
            {
                return;
            }

            var target = GetNormalizedHp(newTarget);
            var fill = hpHolder.Fill.fillAmount;
            var changedFill = hpHolder.ChangedFill.fillAmount;

            var shouldAnimateFill = target > fill;
            var shouldAnimateChangedFill = target < changedFill;

            var nextMode = hpFillMode;
            if (shouldAnimateFill && shouldAnimateChangedFill)
            {
                nextMode = hpFillMode == HpFillMode.Synced
                    ? newTarget >= lastHpTarget
                        ? HpFillMode.FillAnimated
                        : HpFillMode.ChangedFillAnimated
                    : hpFillMode;
            }
            else if (shouldAnimateFill)
            {
                nextMode = HpFillMode.FillAnimated;
            }
            else if (shouldAnimateChangedFill)
            {
                nextMode = HpFillMode.ChangedFillAnimated;
            }
            else
            {
                nextMode = HpFillMode.Synced;
            }

            RebaseAnimatedFill(nextMode, fill, changedFill, target);
            hpFillMode = nextMode;
        }

        private void RebaseAnimatedFill(HpFillMode nextMode, float fill, float changedFill, float target)
        {
            var currentVisual = nextMode switch
            {
                HpFillMode.FillAnimated => fill,
                HpFillMode.ChangedFillAnimated => changedFill,
                _ => target
            };

            hpFiller.Current.Value = currentVisual * statsController.Hp.Max;
        }

        private void OnShowStatsInputChanged(ShowStatsInputMessage message)
        {
            if (statsHolder == null)
            {
                return;
            }

            if (message.IsPressed)
            {
                BeginGlobalHold();
            }
            else
            {
                BeginGlobalReleaseSequence();
            }

            ApplyAllVisualAlphas();
        }

        private void OnStatChanged(StatChangeInfo changeInfo)
        {
            if (statsHolder == null || changeInfo.Source == StatChangeSource.Periodic)
            {
                return;
            }

            if (!statVisibilityStates.TryGetValue(changeInfo.StatType, out var state))
            {
                return;
            }

            StartStatReleaseSequence(state, GetEffectiveRegularAlpha(state));
            ApplyVisualAlpha(changeInfo.StatType);
        }

        private void TickVisibility()
        {
            if (statsHolder == null)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            UpdateGlobalVisibility(deltaTime);

            foreach (var statType in AllStatTypes)
            {
                UpdateStatVisibility(statVisibilityStates[statType], deltaTime);
            }

            ApplyAllVisualAlphas();
        }

        private void InitializeVisibilityState()
        {
            statVisibilityStates.Clear();
            foreach (var statType in AllStatTypes)
            {
                statVisibilityStates[statType] = new StatVisibilityState
                {
                    Alpha = 0f,
                    Phase = VisibilityPhase.Hidden
                };
            }

            globalAlpha = 1f;
            globalStartAlpha = 1f;
            globalTargetAlpha = 1f;
            globalPhaseDuration = 0f;
            globalPhaseElapsed = 0f;
            globalPhase = VisibilityPhase.Holding;
            holdGlobalAtFull = false;
        }

        private void BeginGlobalHold()
        {
            holdGlobalAtFull = true;
            var remainingDuration = GetRemainingRestoreDuration(globalAlpha);
            if (remainingDuration <= 0f)
            {
                globalAlpha = 1f;
                globalPhase = VisibilityPhase.Holding;
                globalPhaseDuration = 0f;
                globalPhaseElapsed = 0f;
                return;
            }

            globalStartAlpha = globalAlpha;
            globalTargetAlpha = 1f;
            globalPhaseDuration = remainingDuration;
            globalPhaseElapsed = 0f;
            globalPhase = VisibilityPhase.Restoring;
        }

        private void BeginGlobalReleaseSequence()
        {
            holdGlobalAtFull = false;
            var remainingDuration = GetRemainingRestoreDuration(globalAlpha);
            if (remainingDuration <= 0f)
            {
                globalAlpha = 1f;
                globalPhase = VisibilityPhase.Showing;
                globalPhaseDuration = statsConfig.ShowTime;
                globalPhaseElapsed = 0f;
                return;
            }

            globalStartAlpha = globalAlpha;
            globalTargetAlpha = 1f;
            globalPhaseDuration = remainingDuration;
            globalPhaseElapsed = 0f;
            globalPhase = VisibilityPhase.Restoring;
        }

        private void UpdateGlobalVisibility(float deltaTime)
        {
            switch (globalPhase)
            {
                case VisibilityPhase.Restoring:
                    globalAlpha = AdvanceAlphaPhase(ref globalPhaseElapsed, globalPhaseDuration, globalStartAlpha, globalTargetAlpha, deltaTime);
                    if (globalPhaseElapsed < globalPhaseDuration)
                    {
                        return;
                    }

                    globalAlpha = 1f;
                    globalPhaseElapsed = 0f;
                    if (holdGlobalAtFull)
                    {
                        globalPhaseDuration = 0f;
                        globalPhase = VisibilityPhase.Holding;
                    }
                    else
                    {
                        globalPhaseDuration = statsConfig.ShowTime;
                        globalPhase = VisibilityPhase.Showing;
                    }
                    return;
                case VisibilityPhase.Showing:
                    globalAlpha = 1f;
                    globalPhaseElapsed += deltaTime;
                    if (globalPhaseElapsed < globalPhaseDuration)
                    {
                        return;
                    }

                    globalPhaseElapsed = 0f;
                    globalPhaseDuration = statsConfig.FadeOutTime;
                    globalStartAlpha = 1f;
                    globalTargetAlpha = 0f;
                    globalPhase = VisibilityPhase.Fading;
                    return;
                case VisibilityPhase.Fading:
                    globalAlpha = AdvanceAlphaPhase(ref globalPhaseElapsed, globalPhaseDuration, globalStartAlpha, globalTargetAlpha, deltaTime);
                    if (globalPhaseElapsed < globalPhaseDuration)
                    {
                        return;
                    }

                    globalAlpha = 0f;
                    globalPhaseElapsed = 0f;
                    globalPhaseDuration = 0f;
                    globalPhase = VisibilityPhase.Hidden;
                    return;
                case VisibilityPhase.Holding:
                    globalAlpha = 1f;
                    return;
                default:
                    globalAlpha = 0f;
                    return;
            }
        }

        private void UpdateCriticalState(StatType statType)
        {
            if (!statVisibilityStates.TryGetValue(statType, out var state))
            {
                return;
            }

            var stat = statsController.GetStat(statType);
            var isCritical = IsCritical(stat);
            if (isCritical == state.IsCritical)
            {
                return;
            }

            state.IsCritical = isCritical;
            if (isCritical)
            {
                state.KeepIconPinnedUntilFade = false;
                StartStatReleaseSequence(state, GetEffectiveRegularAlpha(state));
                return;
            }

            if (state.Phase is VisibilityPhase.Restoring or VisibilityPhase.Showing)
            {
                state.KeepIconPinnedUntilFade = true;
            }
            else
            {
                state.KeepIconPinnedUntilFade = false;
            }
        }

        private void StartStatReleaseSequence(StatVisibilityState state, float currentAlpha)
        {
            var remainingDuration = GetRemainingRestoreDuration(currentAlpha);
            if (remainingDuration <= 0f)
            {
                state.Alpha = 1f;
                state.Phase = VisibilityPhase.Showing;
                state.PhaseDuration = statsConfig.ShowTime;
                state.PhaseElapsed = 0f;
                state.StartAlpha = 1f;
                state.TargetAlpha = 1f;
                return;
            }

            state.Alpha = currentAlpha;
            state.StartAlpha = currentAlpha;
            state.TargetAlpha = 1f;
            state.PhaseDuration = remainingDuration;
            state.PhaseElapsed = 0f;
            state.Phase = VisibilityPhase.Restoring;
        }

        private void UpdateStatVisibility(StatVisibilityState state, float deltaTime)
        {
            switch (state.Phase)
            {
                case VisibilityPhase.Restoring:
                    state.Alpha = AdvanceAlphaPhase(ref state.PhaseElapsed, state.PhaseDuration, state.StartAlpha, state.TargetAlpha, deltaTime);
                    if (state.PhaseElapsed < state.PhaseDuration)
                    {
                        return;
                    }

                    state.Alpha = 1f;
                    state.PhaseElapsed = 0f;
                    state.PhaseDuration = statsConfig.ShowTime;
                    state.Phase = VisibilityPhase.Showing;
                    return;
                case VisibilityPhase.Showing:
                    state.Alpha = 1f;
                    state.PhaseElapsed += deltaTime;
                    if (state.PhaseElapsed < state.PhaseDuration)
                    {
                        return;
                    }

                    state.PhaseElapsed = 0f;
                    state.PhaseDuration = statsConfig.FadeOutTime;
                    state.StartAlpha = 1f;
                    state.TargetAlpha = 0f;
                    state.KeepIconPinnedUntilFade = false;
                    state.Phase = VisibilityPhase.Fading;
                    return;
                case VisibilityPhase.Fading:
                    state.Alpha = AdvanceAlphaPhase(ref state.PhaseElapsed, state.PhaseDuration, state.StartAlpha, state.TargetAlpha, deltaTime);
                    if (state.PhaseElapsed < state.PhaseDuration)
                    {
                        return;
                    }

                    state.Alpha = 0f;
                    state.PhaseElapsed = 0f;
                    state.PhaseDuration = 0f;
                    state.Phase = VisibilityPhase.Hidden;
                    return;
                case VisibilityPhase.Holding:
                    state.Alpha = 1f;
                    return;
                default:
                    state.Alpha = 0f;
                    return;
            }
        }

        private static float AdvanceAlphaPhase(ref float phaseElapsed, float phaseDuration, float startAlpha, float targetAlpha, float deltaTime)
        {
            if (phaseDuration <= 0f)
            {
                phaseElapsed = phaseDuration;
                return targetAlpha;
            }

            phaseElapsed = Mathf.Min(phaseElapsed + deltaTime, phaseDuration);
            var t = phaseElapsed / phaseDuration;
            return Mathf.Lerp(startAlpha, targetAlpha, t);
        }

        private void ApplyAllVisualAlphas()
        {
            foreach (var statType in AllStatTypes)
            {
                ApplyVisualAlpha(statType);
            }
        }

        private void ApplyVisualAlpha(StatType statType)
        {
            var holder = statsHolder?.GetHolder(statType);
            if (holder == null || !statVisibilityStates.TryGetValue(statType, out var state))
            {
                return;
            }

            var regularAlpha = Mathf.Max(globalAlpha, state.Alpha);
            var iconAlpha = regularAlpha;
            if (state.IsCritical || state.KeepIconPinnedUntilFade)
            {
                iconAlpha = 1f;
            }

            SetGraphicAlpha(holder.BackFill, regularAlpha);
            SetGraphicAlpha(holder.Fill, regularAlpha);
            SetGraphicAlpha(holder.ChangedFill, regularAlpha);
            SetGraphicAlpha(holder.Icon, iconAlpha);
        }

        private static void SetGraphicAlpha(UnityEngine.UI.Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            image.color = image.color.WithA(Mathf.Clamp01(alpha));
        }

        private float GetEffectiveRegularAlpha(StatVisibilityState state)
        {
            return Mathf.Max(globalAlpha, state.Alpha);
        }

        private float GetRemainingRestoreDuration(float currentAlpha)
        {
            return statsConfig.AlphaRestoreTime * Mathf.Clamp01(1f - currentAlpha);
        }

        private static bool IsCritical(Stat stat)
        {
            if (Mathf.Approximately(stat.Max, 0f))
            {
                return false;
            }

            var normalizedValue = stat.Value.Value / stat.Max;
            return normalizedValue <= Mathf.Clamp01(stat.MinSafePercent);
        }
    }
}
