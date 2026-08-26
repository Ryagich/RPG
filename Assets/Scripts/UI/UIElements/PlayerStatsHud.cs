using System;
using System.Collections.Generic;
using Inventory.Inventories;
using Inventory.Slot;
using MessagePipe;
using Messages;
using Stats;
using UI.Pages;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace UI.UIElements
{
    /// <summary>
    /// Presents the player HUD consistently on every page that hosts a <see cref="StatsHolder"/>.
    /// The page owns the holder's Unity lifetime; this class owns only subscriptions and HUD state.
    /// </summary>
    internal sealed class PlayerStatsHud : IDisposable
    {
        internal sealed class State
        {
            internal float GlobalAlpha;
            internal float GlobalStartAlpha;
            internal float GlobalTargetAlpha;
            internal float GlobalPhaseDuration;
            internal float GlobalPhaseElapsed;
            internal VisibilityPhase GlobalPhase;
            internal bool IsHoldingGlobalAlpha;
        }

        private static readonly StatType[] AdditionalStatTypes = { StatType.Water, StatType.Food, StatType.Stamina };
        private static readonly StatType[] AllStatTypes = { StatType.Hp, StatType.Water, StatType.Food, StatType.Stamina };

        private enum HpFillMode { Synced, FillAnimated, ChangedFillAnimated }
        internal enum VisibilityPhase { Hidden, Restoring, Showing, Fading, Holding }

        internal sealed class VisibilityState
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

        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly StatFillers statFillers;
        private readonly StatFiller hpFiller;
        private readonly global::Inventory.InventoryConfig inventoryConfig;
        private readonly PlayerInventory playerInventory;
        private readonly CompositeDisposable inputDisposables = new();
        private readonly CompositeDisposable holderDisposables = new();
        private readonly Dictionary<StatType, VisibilityState> statVisibilityStates = new();
        private readonly Dictionary<FastSlotModel, VisibilityState> fastSlotVisibilityStates = new();

        private StatsHolder statsHolder;
        private float lastHpTarget;
        private HpFillMode hpFillMode;
        private float globalAlpha;
        private float globalStartAlpha;
        private float globalTargetAlpha;
        private float globalPhaseDuration;
        private float globalPhaseElapsed;
        private VisibilityPhase globalPhase;
        private bool holdGlobalAtFull;

        public PlayerStatsHud(
            StatsConfig statsConfig,
            StatsController statsController,
            StatFillers statFillers,
            global::Inventory.InventoryConfig inventoryConfig,
            PlayerInventory playerInventory,
            ISubscriber<ShowStatsInputMessage> showStatsInputSubscriber,
            ISubscriber<FastSlotInputMessage> fastSlotInputSubscriber = null)
        {
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.statFillers = statFillers;
            hpFiller = statFillers.Get(StatType.Hp);
            this.inventoryConfig = inventoryConfig;
            this.playerInventory = playerInventory;

            showStatsInputSubscriber.Subscribe(OnShowStatsInputChanged).AddTo(inputDisposables);
            fastSlotInputSubscriber?.Subscribe(OnFastSlotInput).AddTo(inputDisposables);
        }

        public void Attach(StatsHolder holder, State initialState = null)
        {
            Detach();
            statsHolder = holder;
            if (statsHolder == null)
            {
                return;
            }

            InitializeVisibilityState();
            RestoreState(initialState);
            lastHpTarget = statsController.Hp.Value.Value;
            hpFillMode = HpFillMode.Synced;

            hpFiller.Current.Subscribe(_ => RefreshHpFill()).AddTo(holderDisposables);
            statsController.Changed.Subscribe(OnStatChanged).AddTo(holderDisposables);
            statsController.Hp.Value.Subscribe(newTarget =>
            {
                OnHpTargetChanged(newTarget);
                UpdateCriticalState(StatType.Hp);
            }).AddTo(holderDisposables);

            foreach (var statType in AdditionalStatTypes)
            {
                var currentStatType = statType;
                statFillers.Get(currentStatType).Current
                           .Subscribe(_ => RefreshStatFill(currentStatType))
                           .AddTo(holderDisposables);
                statsController.GetStat(currentStatType).Value
                               .Subscribe(_ =>
                               {
                                   RefreshStatFill(currentStatType);
                                   UpdateCriticalState(currentStatType);
                               })
                               .AddTo(holderDisposables);
            }

            Observable.EveryUpdate().Subscribe(_ => TickVisibility()).AddTo(holderDisposables);
            playerInventory.CurrentWeightReactive.Subscribe(UpdateWeightIndicator).AddTo(holderDisposables);
            playerInventory.Changed.Subscribe(_ => DrawFastSlots()).AddTo(holderDisposables);

            RefreshHpFill();
            RefreshAdditionalStatFills();
            UpdateWeightIndicator(playerInventory.CurrentWeight);
            DrawFastSlots();
            foreach (var statType in AllStatTypes)
            {
                UpdateCriticalState(statType);
            }

            if (initialState == null)
            {
                BeginGlobalReleaseSequence();
            }

            ApplyAllVisualAlphas();
        }

        public State CaptureState()
        {
            if (statsHolder == null)
            {
                return null;
            }

            return new State
            {
                GlobalAlpha = globalAlpha,
                GlobalStartAlpha = globalStartAlpha,
                GlobalTargetAlpha = globalTargetAlpha,
                GlobalPhaseDuration = globalPhaseDuration,
                GlobalPhaseElapsed = globalPhaseElapsed,
                GlobalPhase = globalPhase,
                IsHoldingGlobalAlpha = holdGlobalAtFull
            };
        }

        public void Detach()
        {
            holderDisposables.Clear();
            statVisibilityStates.Clear();
            fastSlotVisibilityStates.Clear();
            statsHolder = null;
        }

        public void Dispose()
        {
            Detach();
            inputDisposables.Dispose();
        }

        private void RefreshHpFill()
        {
            var holder = statsHolder?.HPHolder;
            if (holder == null || holder.Fill == null || holder.ChangedFill == null) return;

            var current = GetNormalizedHp(hpFiller.Current.Value);
            var target = GetNormalizedHp(statsController.Hp.Value.Value);
            switch (ResolveHpFillDirection())
            {
                case HpFillMode.FillAnimated:
                    holder.Fill.fillAmount = current;
                    holder.ChangedFill.fillAmount = target;
                    holder.ChangedFill.color = statsConfig.HpRecoveryColor;
                    break;
                case HpFillMode.ChangedFillAnimated:
                    holder.Fill.fillAmount = target;
                    holder.ChangedFill.fillAmount = current;
                    holder.ChangedFill.color = statsConfig.HpDecreaseColor;
                    break;
                default:
                    holder.Fill.fillAmount = target;
                    holder.ChangedFill.fillAmount = target;
                    holder.ChangedFill.color = statsConfig.HpFullColor;
                    break;
            }

            ApplyCriticalColor(holder, statsController.Hp, target);
            ApplyVisualAlpha(StatType.Hp);
        }

        private void RefreshAdditionalStatFills()
        {
            foreach (var statType in AdditionalStatTypes) RefreshStatFill(statType);
        }

        private void RefreshStatFill(StatType statType)
        {
            var holder = statsHolder?.GetHolder(statType);
            if (holder == null || holder.Fill == null || holder.ChangedFill == null) return;

            var stat = statsController.GetStat(statType);
            var current = GetNormalizedStat(stat, statFillers.Get(statType).Current.Value);
            var target = GetNormalizedStat(stat, stat.Value.Value);
            if (target > current)
            {
                holder.Fill.fillAmount = current;
                holder.ChangedFill.fillAmount = target;
                holder.ChangedFill.color = statsConfig.HpRecoveryColor;
            }
            else if (target < current)
            {
                holder.Fill.fillAmount = target;
                holder.ChangedFill.fillAmount = current;
                holder.ChangedFill.color = statsConfig.HpDecreaseColor;
            }
            else
            {
                holder.Fill.fillAmount = target;
                holder.ChangedFill.fillAmount = target;
                holder.ChangedFill.color = statsConfig.HpFullColor;
            }

            ApplyCriticalColor(holder, stat, target);
            ApplyVisualAlpha(statType);
        }

        private void DrawFastSlots()
        {
            if (statsHolder == null) return;
            DrawFastSlot(statsHolder.FastSlot1, playerInventory.FastSlot1);
            DrawFastSlot(statsHolder.FastSlot2, playerInventory.FastSlot2);
            DrawFastSlot(statsHolder.FastSlot3, playerInventory.FastSlot3);
            DrawFastSlot(statsHolder.FastSlot4, playerInventory.FastSlot4);
            ApplyFastSlotAlphas();
        }

        private void DrawFastSlot(SlotView slotView, FastSlotModel model)
        {
            PageUiUtilities.DrawFastSlotItem(slotView, model, playerInventory.HasAnyInventoryItem(model?.ItemConfig));
        }

        private void OnShowStatsInputChanged(ShowStatsInputMessage message)
        {
            if (statsHolder == null) return;
            if (message.IsPressed) BeginGlobalHold(); else BeginGlobalReleaseSequence();
            ApplyAllVisualAlphas();
        }

        private void OnFastSlotInput(FastSlotInputMessage message)
        {
            if (statsHolder == null) return;
            if (!playerInventory.TryGetFastSlot(message.SlotIndex, out var fastSlot)
             || fastSlot?.ItemConfig == null
             || !playerInventory.HasAnyInventoryItem(fastSlot.ItemConfig))
            {
                SignalAllFastSlots();
            }
            else
            {
                SignalFastSlot(fastSlot);
            }

            ApplyFastSlotAlphas();
        }

        private void OnStatChanged(StatChangeInfo changeInfo)
        {
            if (statsHolder == null || changeInfo.Source == StatChangeSource.Periodic
             || !statVisibilityStates.TryGetValue(changeInfo.StatType, out var state)) return;
            StartReleaseSequence(state, GetEffectiveRegularAlpha(state));
            ApplyVisualAlpha(changeInfo.StatType);
        }

        private void OnHpTargetChanged(float newTarget)
        {
            SelectHpFillMode(newTarget);
            lastHpTarget = newTarget;
            RefreshHpFill();
        }

        private void SelectHpFillMode(float newTarget)
        {
            var holder = statsHolder?.HPHolder;
            if (holder == null || holder.Fill == null || holder.ChangedFill == null) return;

            var target = GetNormalizedHp(newTarget);
            var fill = holder.Fill.fillAmount;
            var changedFill = holder.ChangedFill.fillAmount;
            var shouldAnimateFill = target > fill;
            var shouldAnimateChangedFill = target < changedFill;
            if (shouldAnimateFill && shouldAnimateChangedFill)
            {
                hpFillMode = hpFillMode == HpFillMode.Synced
                    ? newTarget >= lastHpTarget ? HpFillMode.FillAnimated : HpFillMode.ChangedFillAnimated
                    : hpFillMode;
            }
            else if (shouldAnimateFill) hpFillMode = HpFillMode.FillAnimated;
            else if (shouldAnimateChangedFill) hpFillMode = HpFillMode.ChangedFillAnimated;
            else hpFillMode = HpFillMode.Synced;

            var currentVisual = hpFillMode switch
            {
                HpFillMode.FillAnimated => fill,
                HpFillMode.ChangedFillAnimated => changedFill,
                _ => target
            };
            hpFiller.Current.Value = currentVisual * statsController.Hp.Max;
        }

        private HpFillMode ResolveHpFillDirection()
        {
            return !Mathf.Approximately(hpFiller.Current.Value, statsController.Hp.Value.Value)
                ? hpFillMode
                : HpFillMode.Synced;
        }

        private void InitializeVisibilityState()
        {
            statVisibilityStates.Clear();
            foreach (var statType in AllStatTypes)
                statVisibilityStates[statType] = new VisibilityState { Alpha = 0f, Phase = VisibilityPhase.Hidden };

            fastSlotVisibilityStates.Clear();
            foreach (var fastSlot in playerInventory.GetFastSlots())
                fastSlotVisibilityStates[fastSlot] = new VisibilityState { Alpha = 0f, Phase = VisibilityPhase.Hidden };

            globalAlpha = globalStartAlpha = globalTargetAlpha = 1f;
            globalPhaseDuration = globalPhaseElapsed = 0f;
            globalPhase = VisibilityPhase.Holding;
            holdGlobalAtFull = false;
        }

        private void RestoreState(State state)
        {
            if (state == null)
            {
                return;
            }

            globalAlpha = state.GlobalAlpha;
            globalStartAlpha = state.GlobalStartAlpha;
            globalTargetAlpha = state.GlobalTargetAlpha;
            globalPhaseDuration = state.GlobalPhaseDuration;
            globalPhaseElapsed = state.GlobalPhaseElapsed;
            globalPhase = state.GlobalPhase;
            holdGlobalAtFull = state.IsHoldingGlobalAlpha;
        }

        private void BeginGlobalHold()
        {
            holdGlobalAtFull = true;
            StartGlobalRestore();
        }

        private void BeginGlobalReleaseSequence()
        {
            holdGlobalAtFull = false;
            StartGlobalRestore();
        }

        private void StartGlobalRestore()
        {
            var duration = GetRemainingRestoreDuration(globalAlpha);
            if (duration <= 0f)
            {
                globalAlpha = 1f;
                globalPhase = holdGlobalAtFull ? VisibilityPhase.Holding : VisibilityPhase.Showing;
                globalPhaseDuration = holdGlobalAtFull ? 0f : statsConfig.ShowTime;
                globalPhaseElapsed = 0f;
                return;
            }

            globalStartAlpha = globalAlpha;
            globalTargetAlpha = 1f;
            globalPhaseDuration = duration;
            globalPhaseElapsed = 0f;
            globalPhase = VisibilityPhase.Restoring;
        }

        private void TickVisibility()
        {
            if (statsHolder == null) return;
            var deltaTime = Time.deltaTime;
            UpdateGlobalVisibility(deltaTime);
            foreach (var state in statVisibilityStates.Values) UpdateVisibility(state, deltaTime);
            foreach (var state in fastSlotVisibilityStates.Values) UpdateVisibility(state, deltaTime);
            ApplyAllVisualAlphas();
        }

        private void UpdateGlobalVisibility(float deltaTime)
        {
            switch (globalPhase)
            {
                case VisibilityPhase.Restoring:
                    globalAlpha = Advance(ref globalPhaseElapsed, globalPhaseDuration, globalStartAlpha, globalTargetAlpha, deltaTime);
                    if (globalPhaseElapsed < globalPhaseDuration) return;
                    globalAlpha = 1f;
                    globalPhaseElapsed = 0f;
                    globalPhase = holdGlobalAtFull ? VisibilityPhase.Holding : VisibilityPhase.Showing;
                    globalPhaseDuration = holdGlobalAtFull ? 0f : statsConfig.ShowTime;
                    return;
                case VisibilityPhase.Showing:
                    globalAlpha = 1f;
                    globalPhaseElapsed += deltaTime;
                    if (globalPhaseElapsed < globalPhaseDuration) return;
                    globalPhaseElapsed = 0f;
                    globalPhaseDuration = statsConfig.FadeOutTime;
                    globalStartAlpha = 1f;
                    globalTargetAlpha = 0f;
                    globalPhase = VisibilityPhase.Fading;
                    return;
                case VisibilityPhase.Fading:
                    globalAlpha = Advance(ref globalPhaseElapsed, globalPhaseDuration, globalStartAlpha, globalTargetAlpha, deltaTime);
                    if (globalPhaseElapsed < globalPhaseDuration) return;
                    globalAlpha = 0f;
                    globalPhaseElapsed = globalPhaseDuration = 0f;
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
            if (!statVisibilityStates.TryGetValue(statType, out var state)) return;
            var isCritical = IsCritical(statsController.GetStat(statType));
            if (isCritical == state.IsCritical) return;
            state.IsCritical = isCritical;
            if (isCritical)
            {
                state.KeepIconPinnedUntilFade = false;
                StartReleaseSequence(state, GetEffectiveRegularAlpha(state));
            }
            else
            {
                state.KeepIconPinnedUntilFade = state.Phase is VisibilityPhase.Restoring or VisibilityPhase.Showing;
            }
        }

        private void StartReleaseSequence(VisibilityState state, float currentAlpha)
        {
            var duration = GetRemainingRestoreDuration(currentAlpha);
            state.Alpha = currentAlpha;
            state.StartAlpha = currentAlpha;
            state.TargetAlpha = 1f;
            state.PhaseDuration = duration;
            state.PhaseElapsed = 0f;
            state.Phase = duration <= 0f ? VisibilityPhase.Showing : VisibilityPhase.Restoring;
            if (duration <= 0f)
            {
                state.Alpha = 1f;
                state.StartAlpha = state.TargetAlpha = 1f;
                state.PhaseDuration = statsConfig.ShowTime;
            }
        }

        private void UpdateVisibility(VisibilityState state, float deltaTime)
        {
            switch (state.Phase)
            {
                case VisibilityPhase.Restoring:
                    state.Alpha = Advance(ref state.PhaseElapsed, state.PhaseDuration, state.StartAlpha, state.TargetAlpha, deltaTime);
                    if (state.PhaseElapsed < state.PhaseDuration) return;
                    state.Alpha = 1f;
                    state.PhaseElapsed = 0f;
                    state.PhaseDuration = statsConfig.ShowTime;
                    state.Phase = VisibilityPhase.Showing;
                    return;
                case VisibilityPhase.Showing:
                    state.Alpha = 1f;
                    state.PhaseElapsed += deltaTime;
                    if (state.PhaseElapsed < state.PhaseDuration) return;
                    state.PhaseElapsed = 0f;
                    state.PhaseDuration = statsConfig.FadeOutTime;
                    state.StartAlpha = 1f;
                    state.TargetAlpha = 0f;
                    state.KeepIconPinnedUntilFade = false;
                    state.Phase = VisibilityPhase.Fading;
                    return;
                case VisibilityPhase.Fading:
                    state.Alpha = Advance(ref state.PhaseElapsed, state.PhaseDuration, state.StartAlpha, state.TargetAlpha, deltaTime);
                    if (state.PhaseElapsed < state.PhaseDuration) return;
                    state.Alpha = 0f;
                    state.PhaseElapsed = state.PhaseDuration = 0f;
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

        private void ApplyAllVisualAlphas()
        {
            foreach (var statType in AllStatTypes) ApplyVisualAlpha(statType);
            ApplyFastSlotAlphas();
        }

        private void ApplyVisualAlpha(StatType statType)
        {
            var holder = statsHolder?.GetHolder(statType);
            if (holder == null || !statVisibilityStates.TryGetValue(statType, out var state)) return;
            var alpha = Mathf.Max(globalAlpha, state.Alpha);
            SetGraphicAlpha(holder.BackFill, alpha);
            SetGraphicAlpha(holder.Fill, alpha);
            SetGraphicAlpha(holder.ChangedFill, alpha);
            SetGraphicAlpha(holder.Icon, state.IsCritical || state.KeepIconPinnedUntilFade ? 1f : alpha);
        }

        private void ApplyFastSlotAlphas()
        {
            if (statsHolder == null) return;
            ApplyFastSlotAlpha(statsHolder.FastSlot1, playerInventory.FastSlot1);
            ApplyFastSlotAlpha(statsHolder.FastSlot2, playerInventory.FastSlot2);
            ApplyFastSlotAlpha(statsHolder.FastSlot3, playerInventory.FastSlot3);
            ApplyFastSlotAlpha(statsHolder.FastSlot4, playerInventory.FastSlot4);
        }

        private void ApplyFastSlotAlpha(SlotView slotView, FastSlotModel model)
        {
            if (slotView == null || model == null || !fastSlotVisibilityStates.TryGetValue(model, out var state)) return;
            if (!slotView.TryGetComponent<CanvasGroup>(out var canvasGroup)) canvasGroup = slotView.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = Mathf.Max(globalAlpha, state.Alpha);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void SignalAllFastSlots()
        {
            foreach (var state in fastSlotVisibilityStates.Values) StartReleaseSequence(state, GetEffectiveRegularAlpha(state));
        }

        private void SignalFastSlot(FastSlotModel fastSlot)
        {
            if (fastSlot != null && fastSlotVisibilityStates.TryGetValue(fastSlot, out var state))
                StartReleaseSequence(state, GetEffectiveRegularAlpha(state));
        }

        private void UpdateWeightIndicator(float _)
        {
            var indicator = statsHolder?.WeightIndicator;
            if (indicator == null) return;
            var percent = playerInventory.CurrentWeightPercent;
            if (percent >= inventoryConfig.WeightBlocksMovementPercent)
            {
                indicator.enabled = true;
                indicator.color = statsConfig.HpDecreaseColor;
            }
            else if (percent > inventoryConfig.WeightAffectsMovementPercent)
            {
                indicator.enabled = true;
                indicator.color = statsConfig.Warning;
            }
            else indicator.enabled = false;
        }

        private void ApplyCriticalColor(StatHolder holder, Stat stat, float normalizedTarget)
        {
            var threshold = stat is SafeStat safeStat ? Mathf.Clamp01(safeStat.MinSafePercent) : 0f;
            var color = normalizedTarget >= threshold
                ? statsConfig.HpFullColor
                : Color.Lerp(statsConfig.HpDecreaseColor, statsConfig.HpFullColor, threshold <= 0f ? 0f : normalizedTarget / threshold);
            holder.Fill.color = color;
            if (holder.Icon != null) holder.Icon.color = color;
        }

        private float GetEffectiveRegularAlpha(VisibilityState state) => Mathf.Max(globalAlpha, state.Alpha);
        private float GetRemainingRestoreDuration(float alpha) => statsConfig.AlphaRestoreTime * Mathf.Clamp01(1f - alpha);
        private float GetNormalizedHp(float value) => Mathf.Approximately(statsController.Hp.Max, 0f) ? 0f : value / statsController.Hp.Max;
        private static float GetNormalizedStat(Stat stat, float value) => Mathf.Approximately(stat.Max, 0f) ? 0f : value / stat.Max;
        private static float Advance(ref float elapsed, float duration, float from, float to, float deltaTime)
        {
            if (duration <= 0f) { elapsed = duration; return to; }
            elapsed = Mathf.Min(elapsed + deltaTime, duration);
            return Mathf.Lerp(from, to, elapsed / duration);
        }

        private static void SetGraphicAlpha(Image image, float alpha)
        {
            if (image != null) image.color = image.color.WithA(Mathf.Clamp01(alpha));
        }

        private static bool IsCritical(Stat stat)
        {
            return stat is SafeStat safeStat
                   && !Mathf.Approximately(stat.Max, 0f)
                   && stat.Value.Value / stat.Max <= Mathf.Clamp01(safeStat.MinSafePercent);
        }
    }
}
