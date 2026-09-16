using System;
using Inventory.Item;

namespace Inventory
{
    /// <summary>
    /// Owns the visual and Animator-facing half of a player's weapon lifecycle. It does not
    /// decide which weapon the player wants or whether gameplay currently permits a change;
    /// its caller supplies the desired selection and reconciles it at valid gameplay boundaries.
    /// </summary>
    internal sealed class PlayerWeaponTransitionController
    {
        private const string BeginMoveWeaponToRightHandEventName = "BeginMoveWeaponToRightHand";
        private const string TakeWeaponInHandEventName = "TakeWeaponInHand";
        private const string BeginMoveWeaponToBeltEventName = "BeginMoveWeaponToBelt";
        private const string PutWeaponOnBeltEventName = "PutWeaponOnBelt";

        private readonly PlayerWeaponVisualController weaponVisual;
        private readonly PlayerWeaponTransitionAnimator weaponTransitionAnimator;
        private readonly PlayerWeaponTransitionState transitionState = new();
        private readonly Action endDamageWindow;

        public PlayerWeaponTransitionController(
            PlayerWeaponVisualController weaponVisual,
            PlayerWeaponTransitionAnimator weaponTransitionAnimator,
            Action endDamageWindow)
        {
            this.weaponVisual = weaponVisual;
            this.weaponTransitionAnimator = weaponTransitionAnimator;
            this.endDamageWindow = endDamageWindow;
        }

        public bool IsAnimationInProgress => transitionState.IsAnimationInProgress;
        public WeaponAnimationKind CurrentAnimationKind => transitionState.CurrentKind;
        public WeaponDisplayMode DisplayMode => weaponVisual.DisplayMode;
        public ItemConfig CurrentItemConfig => weaponVisual.ItemConfig;
        public int CurrentSlotIndex => weaponVisual.SlotIndex;
        public bool IsSheathed => !IsAnimationInProgress && DisplayMode != WeaponDisplayMode.RightHand;

        public bool IsSelectionInHand(PlayerWeaponSelection selection)
        {
            return selection.HasWeapon
                   && DisplayMode == WeaponDisplayMode.RightHand
                   && selection.Matches(CurrentSlotIndex, CurrentItemConfig);
        }

        public void ResetAnimatorRequests()
        {
            weaponTransitionAnimator.ResetRequests();
        }

        public WeaponTransitionOutcome Reconcile(PlayerWeaponSelection selection, bool isDrawRequested)
        {
            if (IsAnimationInProgress)
            {
                return WeaponTransitionOutcome.None;
            }

            if (!selection.HasWeapon)
            {
                return ReconcileWithoutSelectedWeapon();
            }

            return isDrawRequested
                ? ReconcileDrawnWeapon(selection)
                : ReconcileHolsteredWeapon(selection);
        }

        public WeaponTransitionOutcome RequestSheathe()
        {
            return StartSheatheAnimation(CurrentSlotIndex, CurrentItemConfig);
        }

        public bool TryApplyWeaponSlotIntentDuringTransition(
            PlayerWeaponSelection selection,
            bool isDrawRequested,
            out WeaponTransitionOutcome outcome)
        {
            outcome = WeaponTransitionOutcome.None;
            if (!IsAnimationInProgress)
            {
                return false;
            }

            switch (CurrentAnimationKind)
            {
                case WeaponAnimationKind.Sheathe:
                    if (!isDrawRequested || !selection.HasWeapon)
                    {
                        return true;
                    }

                    if (selection.Matches(CurrentSlotIndex, CurrentItemConfig))
                    {
                        outcome = StartDrawAnimation(selection, preserveCurrentVisual: true);
                        return true;
                    }

                    TryStartNextWeaponDuringSheatheIfBeginEventPassed(selection, isDrawRequested, out outcome);
                    return true;

                case WeaponAnimationKind.Draw:
                    if (isDrawRequested && IsSelectionInHand(selection))
                    {
                        return true;
                    }

                    outcome = StartSheatheAnimation(CurrentSlotIndex, CurrentItemConfig);
                    return true;

                default:
                    return false;
            }
        }

        public WeaponTransitionOutcome BeginMoveWeaponToRightHandFromAnimationEvent()
        {
            if (!weaponTransitionAnimator.IsStateExpectedForAnimationEvent(WeaponAnimationKind.Draw))
            {
                return WeaponTransitionOutcome.None;
            }

            SynchronizeWithAnimationEvent(
                WeaponAnimationKind.Draw,
                preservePoseForDraw: weaponVisual.IsAttachedTo(WeaponDisplayMode.RightHand));

            if (!transitionState.TryBeginAttachmentBlend(WeaponAnimationKind.Draw))
            {
                return WeaponTransitionOutcome.None;
            }

            if (transitionState.ShouldPreservePoseForDraw)
            {
                weaponVisual.MovePreservingPose(WeaponDisplayMode.RightHand);
                weaponVisual.CleanupExceptCurrent();
                return WeaponTransitionOutcome.None;
            }

            weaponVisual.StartAttachmentBlend(
                WeaponDisplayMode.RightHand,
                weaponTransitionAnimator.GetClip(CurrentAnimationKind),
                GetCurrentAnimationStateHash(),
                BeginMoveWeaponToRightHandEventName,
                TakeWeaponInHandEventName);
            weaponVisual.CleanupExceptCurrent();
            return WeaponTransitionOutcome.None;
        }

        public WeaponTransitionOutcome TakeWeaponInHandFromAnimationEvent()
        {
            if (!weaponTransitionAnimator.IsStateExpectedForAnimationEvent(WeaponAnimationKind.Draw)
                || CurrentItemConfig == null)
            {
                return WeaponTransitionOutcome.None;
            }

            SynchronizeWithAnimationEvent(WeaponAnimationKind.Draw);
            weaponVisual.FinalizeRender(
                CurrentItemConfig,
                CurrentSlotIndex,
                WeaponDisplayMode.RightHand,
                snapToAttachmentTransform: true);
            weaponVisual.CleanupExceptCurrent();
            return CompleteAnimation(WeaponAnimationKind.Draw);
        }

        public WeaponTransitionOutcome BeginMoveWeaponToBeltFromAnimationEvent(
            PlayerWeaponSelection selection,
            bool isDrawRequested)
        {
            if (!weaponTransitionAnimator.IsStateExpectedForAnimationEvent(WeaponAnimationKind.Sheathe))
            {
                return WeaponTransitionOutcome.None;
            }

            SynchronizeWithAnimationEvent(WeaponAnimationKind.Sheathe);
            if (!transitionState.TryBeginAttachmentBlend(WeaponAnimationKind.Sheathe))
            {
                return WeaponTransitionOutcome.None;
            }

            if (TryStartNextWeaponDuringSheathe(selection, isDrawRequested, out var outcome))
            {
                return outcome;
            }

            weaponVisual.MovePreservingPose(WeaponDisplayMode.Belt);
            return WeaponTransitionOutcome.None;
        }

        public WeaponTransitionOutcome PutWeaponOnBeltFromAnimationEvent(PlayerWeaponSelection selection)
        {
            if (!weaponTransitionAnimator.IsStateExpectedForAnimationEvent(WeaponAnimationKind.Sheathe)
                || CurrentItemConfig == null)
            {
                return WeaponTransitionOutcome.None;
            }

            SynchronizeWithAnimationEvent(WeaponAnimationKind.Sheathe);
            weaponVisual.FinalizeRender(
                CurrentItemConfig,
                CurrentSlotIndex,
                WeaponDisplayMode.Belt,
                snapToAttachmentTransform: false);
            weaponVisual.CleanupExceptCurrent();

            if (!selection.HasWeapon)
            {
                RenderWeapon(null, 0, WeaponDisplayMode.None);
                return CompleteAnimation(WeaponAnimationKind.Sheathe);
            }

            if (!selection.Matches(CurrentSlotIndex, CurrentItemConfig))
            {
                RenderWeapon(selection.ItemConfig, selection.SlotIndex, WeaponDisplayMode.Belt);
            }

            return CompleteAnimation(WeaponAnimationKind.Sheathe);
        }

        public WeaponTransitionOutcome SynchronizeSheatheCompletionWithAnimatorState()
        {
            if (!IsAnimationInProgress
                || CurrentAnimationKind != WeaponAnimationKind.Sheathe
                || !weaponTransitionAnimator.IsAvailable)
            {
                return WeaponTransitionOutcome.None;
            }

            if (weaponTransitionAnimator.IsStateActive(WeaponAnimationKind.Sheathe))
            {
                transitionState.MarkSheatheStateEntered();
                return WeaponTransitionOutcome.None;
            }

            if (!transitionState.CanSynchronizeSheathe()
                || CurrentItemConfig == null
                || !weaponVisual.IsAttachedTo(WeaponDisplayMode.Belt))
            {
                return WeaponTransitionOutcome.None;
            }

            weaponVisual.FinalizeRender(
                CurrentItemConfig,
                CurrentSlotIndex,
                WeaponDisplayMode.Belt,
                snapToAttachmentTransform: false);
            return CompleteAnimation(WeaponAnimationKind.Sheathe);
        }

        public bool TryReverseForFullBodyAction(out bool isDrawRequested)
        {
            isDrawRequested = false;
            if (weaponVisual.IsAttachedTo(WeaponDisplayMode.RightHand)
                && weaponTransitionAnimator.IsStateActive(WeaponAnimationKind.Sheathe))
            {
                weaponTransitionAnimator.Request(WeaponAnimationKind.Draw);
                isDrawRequested = true;
                return true;
            }

            if (weaponVisual.IsAttachedTo(WeaponDisplayMode.Belt)
                && weaponTransitionAnimator.IsStateActive(WeaponAnimationKind.Draw))
            {
                weaponTransitionAnimator.Request(WeaponAnimationKind.Sheathe);
                return true;
            }

            return false;
        }

        public bool TryGetCurrentWeaponPose(out UnityEngine.Vector3 position, out UnityEngine.Quaternion rotation)
        {
            return weaponVisual.TryGetPose(out position, out rotation);
        }

        public void Dispose()
        {
            weaponVisual.Dispose();
        }

        private WeaponTransitionOutcome ReconcileWithoutSelectedWeapon()
        {
            if (DisplayMode == WeaponDisplayMode.RightHand && CurrentItemConfig != null)
            {
                return StartSheatheAnimation(CurrentSlotIndex, CurrentItemConfig);
            }

            RenderWeapon(null, 0, WeaponDisplayMode.None);
            return WeaponTransitionOutcome.None;
        }

        private WeaponTransitionOutcome ReconcileDrawnWeapon(PlayerWeaponSelection selection)
        {
            if (DisplayMode == WeaponDisplayMode.RightHand)
            {
                return IsSelectionInHand(selection)
                    ? WeaponTransitionOutcome.None
                    : StartSheatheAnimation(CurrentSlotIndex, CurrentItemConfig);
            }

            if (!IsCurrentPresentation(selection, WeaponDisplayMode.Belt))
            {
                RenderWeapon(selection.ItemConfig, selection.SlotIndex, WeaponDisplayMode.Belt);
            }

            return StartDrawAnimation(selection);
        }

        private WeaponTransitionOutcome ReconcileHolsteredWeapon(PlayerWeaponSelection selection)
        {
            if (DisplayMode == WeaponDisplayMode.RightHand && CurrentItemConfig != null)
            {
                return StartSheatheAnimation(CurrentSlotIndex, CurrentItemConfig);
            }

            if (!IsCurrentPresentation(selection, WeaponDisplayMode.Belt))
            {
                RenderWeapon(selection.ItemConfig, selection.SlotIndex, WeaponDisplayMode.Belt);
            }

            return WeaponTransitionOutcome.None;
        }

        private WeaponTransitionOutcome StartDrawAnimation(
            PlayerWeaponSelection selection,
            bool preserveCurrentVisual = false)
        {
            if (!selection.HasWeapon)
            {
                return WeaponTransitionOutcome.None;
            }

            var canPreserveCurrentVisual =
                preserveCurrentVisual
                && weaponVisual.Instance != null
                && selection.Matches(CurrentSlotIndex, CurrentItemConfig)
                && DisplayMode != WeaponDisplayMode.None;

            if (!canPreserveCurrentVisual && !IsCurrentPresentation(selection, WeaponDisplayMode.Belt))
            {
                RenderWeapon(selection.ItemConfig, selection.SlotIndex, WeaponDisplayMode.Belt);
            }

            transitionState.Begin(WeaponAnimationKind.Draw, preserveCurrentVisual);
            if (!weaponTransitionAnimator.IsAvailable)
            {
                weaponVisual.FinalizeRender(
                    selection.ItemConfig,
                    selection.SlotIndex,
                    WeaponDisplayMode.RightHand,
                    snapToAttachmentTransform: true);
                return CompleteAnimation(WeaponAnimationKind.Draw);
            }

            weaponTransitionAnimator.Request(WeaponAnimationKind.Draw);
            return WeaponTransitionOutcome.None;
        }

        private WeaponTransitionOutcome StartSheatheAnimation(int slotIndex, ItemConfig itemConfig)
        {
            if (itemConfig == null)
            {
                RenderWeapon(null, 0, WeaponDisplayMode.None);
                return WeaponTransitionOutcome.None;
            }

            transitionState.Begin(WeaponAnimationKind.Sheathe);
            if (!weaponTransitionAnimator.IsAvailable)
            {
                weaponVisual.FinalizeRender(
                    itemConfig,
                    slotIndex,
                    WeaponDisplayMode.Belt,
                    snapToAttachmentTransform: false);
                return CompleteAnimation(WeaponAnimationKind.Sheathe);
            }

            weaponTransitionAnimator.Request(WeaponAnimationKind.Sheathe);
            return WeaponTransitionOutcome.None;
        }

        private bool TryStartNextWeaponDuringSheathe(
            PlayerWeaponSelection selection,
            bool isDrawRequested,
            out WeaponTransitionOutcome outcome)
        {
            outcome = WeaponTransitionOutcome.None;
            if (!ShouldSwapWeaponOnBeginMoveToBeltEvent(selection, isDrawRequested))
            {
                return false;
            }

            DestroyCurrentWeaponInstance();
            RenderWeapon(selection.ItemConfig, selection.SlotIndex, WeaponDisplayMode.Belt);
            outcome = StartDrawAnimation(selection, preserveCurrentVisual: true);
            return true;
        }

        private bool TryStartNextWeaponDuringSheatheIfBeginEventPassed(
            PlayerWeaponSelection selection,
            bool isDrawRequested,
            out WeaponTransitionOutcome outcome)
        {
            outcome = WeaponTransitionOutcome.None;
            if (!weaponTransitionAnimator.HasReachedEvent(
                    WeaponAnimationKind.Sheathe,
                    BeginMoveWeaponToBeltEventName))
            {
                return false;
            }

            return TryStartNextWeaponDuringSheathe(selection, isDrawRequested, out outcome);
        }

        private bool ShouldSwapWeaponOnBeginMoveToBeltEvent(
            PlayerWeaponSelection selection,
            bool isDrawRequested)
        {
            return CurrentAnimationKind == WeaponAnimationKind.Sheathe
                   && DisplayMode == WeaponDisplayMode.RightHand
                   && CurrentItemConfig != null
                   && isDrawRequested
                   && selection.HasWeapon
                   && !selection.Matches(CurrentSlotIndex, CurrentItemConfig);
        }

        private WeaponTransitionOutcome CompleteAnimation(WeaponAnimationKind expectedKind)
        {
            if (!transitionState.Complete(expectedKind))
            {
                return WeaponTransitionOutcome.None;
            }

            weaponTransitionAnimator.ResetRequests();
            return expectedKind == WeaponAnimationKind.Draw
                ? WeaponTransitionOutcome.Drawn
                : WeaponTransitionOutcome.Sheathed;
        }

        private void SynchronizeWithAnimationEvent(
            WeaponAnimationKind animationKind,
            bool preservePoseForDraw = false)
        {
            if (CurrentAnimationKind != animationKind)
            {
                transitionState.Begin(animationKind, preservePoseForDraw);
            }
        }

        private bool IsCurrentPresentation(PlayerWeaponSelection selection, WeaponDisplayMode displayMode)
        {
            return selection.Matches(CurrentSlotIndex, CurrentItemConfig)
                   && DisplayMode == displayMode
                   && weaponVisual.Instance != null;
        }

        private void RenderWeapon(ItemConfig itemConfig, int slotIndex, WeaponDisplayMode displayMode)
        {
            endDamageWindow?.Invoke();
            weaponVisual.Render(itemConfig, slotIndex, displayMode);
        }

        private void DestroyCurrentWeaponInstance()
        {
            endDamageWindow?.Invoke();
            weaponVisual.Destroy();
        }

        private int GetCurrentAnimationStateHash()
        {
            return CurrentAnimationKind == WeaponAnimationKind.None
                ? 0
                : weaponTransitionAnimator.GetStateHash(CurrentAnimationKind);
        }
    }

    internal enum WeaponTransitionOutcome
    {
        None,
        Drawn,
        Sheathed
    }
}
