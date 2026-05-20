using System;
using GameModes;
using Inventory.Inventories;
using Inventory.Item;
using MessagePipe;
using Messages;
using Movement;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Inventory
{
    public sealed class PlayerWeaponInHandController : IStartable, ITickable, IDisposable
    {
        private const string WeaponAnimationLayerName = "Weapon Layers";
        private const string DrawWeaponStatePath = "Weapon Layers.DrawWeapon";
        private const string SheatheWeaponStatePath = "Weapon Layers.SheatheWeapon";
        private const string HoldAttackWindUpStateName = "A_Attack_LightCombo01C_WindUp_Sword";
        private const string DrawWeaponClipName = "A_Draw_Sword";
        private const string SheatheWeaponClipName = "A_Sheathe_Sword";
        private const float FallbackAttachmentBlendDuration = 0.08f;
        private const float HoldAttackReadyFallbackNormalizedTime = 0.98f;
        private const string BeginMoveWeaponToRightHandEventName = "BeginMoveWeaponToRightHand";
        private const string TakeWeaponInHandEventName = "TakeWeaponInHand";
        private const string BeginMoveWeaponToBeltEventName = "BeginMoveWeaponToBelt";
        private const string PutWeaponOnBeltEventName = "PutWeaponOnBelt";
        private const string DrawWeaponRequestedParameter = "MoveWeaponInHand";
        private const string SheatheWeaponRequestedParameter = "MoveWeaponInBelt";
        private const string HoldAttackRequestedParameter = "HoldAttack";
        private const string AttackRequestedParameter = "Attack";
        private const string LeftClickLogPrefix = "[WeaponLmb]";

        private static readonly int DrawWeaponStateHash = Animator.StringToHash(DrawWeaponStatePath);
        private static readonly int SheatheWeaponStateHash = Animator.StringToHash(SheatheWeaponStatePath);
        private static readonly int HoldAttackWindUpStateShortNameHash = Animator.StringToHash(HoldAttackWindUpStateName);
        private static readonly int DrawWeaponRequestedParameterHash = Animator.StringToHash(DrawWeaponRequestedParameter);
        private static readonly int SheatheWeaponRequestedParameterHash = Animator.StringToHash(SheatheWeaponRequestedParameter);
        private static readonly int HoldAttackRequestedParameterHash = Animator.StringToHash(HoldAttackRequestedParameter);
        private static readonly int AttackRequestedParameterHash = Animator.StringToHash(AttackRequestedParameter);

        private enum WeaponDisplayMode
        {
            None,
            RightHand,
            Belt
        }

        private enum WeaponAnimationKind
        {
            None,
            Draw,
            Sheathe
        }

        private readonly PlayerInventory playerInventory;
        private readonly PlayerWeaponHandAnchor handAnchor;
        private readonly PlayerWeaponAnimationEventReceiver animationEventReceiver;
        private readonly Animator animator;
        private readonly GameModesController gameModesController;
        private readonly PlayerMovement playerMovement;
        private readonly PlayerAnimationController playerAnimationController;
        private readonly CompositeDisposable disposables = new();
        private readonly SerialDisposable weaponAttachmentBlendDisposable = new();
        private readonly int weaponAnimationLayerIndex;

        private int selectedWeaponSlotIndex = 1;
        private bool isWeaponDrawn = true;
        private bool isWeaponAnimationInProgress;
        private bool hasPendingRefresh;
        private bool isHoldAttackActive;
        private bool isHoldAttackReady;
        private bool isAttackReleaseQueued;
        private bool isHitAttackInProgress;
        private GameObject currentWeaponInstance;
        private ItemConfig currentWeaponItemConfig;
        private ItemConfig lastObservedSelectedSlotItemConfig;
        private int currentRenderedSlotIndex;
        private WeaponDisplayMode currentDisplayMode;
        private WeaponDisplayMode pendingAnimationTransferMode;
        private WeaponAnimationKind currentAnimationKind;
        private Action pendingAnimationTransferAction;
        private string currentAnimationClipName;

        public PlayerWeaponInHandController(
            PlayerInventory playerInventory,
            PlayerWeaponHandAnchor handAnchor,
            PlayerWeaponAnimationEventReceiver animationEventReceiver,
            Animator animator,
            GameModesController gameModesController,
            PlayerMovement playerMovement,
            PlayerAnimationController playerAnimationController,
            ISubscriber<WeaponSlotInputMessage> weaponSlotInputSubscriber,
            ISubscriber<MouseDown> mouseDownSubscriber,
            ISubscriber<MouseUp> mouseUpSubscriber,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.playerInventory = playerInventory;
            this.handAnchor = handAnchor;
            this.animationEventReceiver = animationEventReceiver;
            this.animator = animator;
            this.gameModesController = gameModesController;
            this.playerMovement = playerMovement;
            this.playerAnimationController = playerAnimationController;
            weaponAnimationLayerIndex = animator != null
                ? animator.GetLayerIndex(WeaponAnimationLayerName)
                : -1;

            animationEventReceiver?.Bind(this);

            weaponSlotInputSubscriber.Subscribe(OnWeaponSlotInput).AddTo(disposables);
            mouseDownSubscriber.Subscribe(OnMouseDown).AddTo(disposables);
            mouseUpSubscriber.Subscribe(OnMouseUp).AddTo(disposables);
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged).AddTo(disposables);
            playerInventory.Changed.Subscribe(_ => RefreshWeaponInHand()).AddTo(disposables);
            playerInventory.HandSlot.Subscribe(_ => RefreshWeaponInHand()).AddTo(disposables);
        }

        public void Start()
        {
            ResetAnimatorRequests();
            RefreshWeaponInHand();
        }

        public void Dispose()
        {
            weaponAttachmentBlendDisposable.Dispose();
            DestroyCurrentWeaponInstance();
            disposables.Dispose();
        }

        public void Tick()
        {
            if (!isHoldAttackActive
             || isHoldAttackReady
             || !isAttackReleaseQueued
             || isHitAttackInProgress
             || animator == null)
            {
                return;
            }

            if (!TryGetHoldAttackWindUpStateInfo(out var stateInfo, out var layerIndex))
            {
                return;
            }

            if (stateInfo.normalizedTime < HoldAttackReadyFallbackNormalizedTime)
            {
                return;
            }

            isHoldAttackReady = true;
            LogLeftClick($"hold attack ready fallback by normalizedTime | layer={layerIndex} norm={stateInfo.normalizedTime:0.00}");
            TriggerAttack();
        }

        public void BeginMoveWeaponToRightHandFromAnimationEvent()
        {
            StartWeaponAttachmentBlend(
                WeaponDisplayMode.RightHand,
                BeginMoveWeaponToRightHandEventName,
                TakeWeaponInHandEventName);
        }

        public void TakeWeaponInHandFromAnimationEvent()
        {
            ApplyPendingAnimationTransfer(WeaponDisplayMode.RightHand);
            CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Draw);
        }

        public void BeginMoveWeaponToBeltFromAnimationEvent()
        {
            StartWeaponAttachmentBlend(
                WeaponDisplayMode.Belt,
                BeginMoveWeaponToBeltEventName,
                PutWeaponOnBeltEventName);
        }

        public void PutWeaponOnBeltFromAnimationEvent()
        {
            ApplyPendingAnimationTransfer(WeaponDisplayMode.Belt);
            CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Sheathe);
        }

        public void HoldAttackReadyFromAnimationEvent()
        {
            if (!isHoldAttackActive)
            {
                return;
            }

            isHoldAttackReady = true;
            LogLeftClick("hold attack ready event");
            TryConsumeQueuedAttackRelease();
        }

        public void AttackStartedFromAnimationEvent()
        {
            isHoldAttackActive = false;
            isHoldAttackReady = false;
            isAttackReleaseQueued = false;
            isHitAttackInProgress = true;

            playerMovement?.ChangeState(false);
            playerAnimationController?.SetLocomotionLocked(true);
            LogLeftClick("attack started event");
        }

        public void AttackFinishedFromAnimationEvent()
        {
            isHitAttackInProgress = false;

            if (gameModesController.GameMode == GameMode.Game)
            {
                playerMovement?.ChangeState(true);
            }

            playerAnimationController?.SetLocomotionLocked(false);
            LogLeftClick("attack finished event");
            RefreshWeaponInHand();
        }

        private void OnWeaponSlotInput(WeaponSlotInputMessage message)
        {
            if (message.SlotIndex is < 1 or > 2 || isHoldAttackActive || isHitAttackInProgress)
            {
                return;
            }

            if (selectedWeaponSlotIndex == message.SlotIndex)
            {
                var selectedItemConfig = GetSelectedWeaponItemConfig();
                if (selectedItemConfig == null)
                {
                    RefreshWeaponInHand();
                    return;
                }

                isWeaponDrawn = !isWeaponDrawn;
                InterruptCurrentWeaponAnimationIfNeeded();
                RefreshWeaponInHand();
                return;
            }

            selectedWeaponSlotIndex = message.SlotIndex;
            isWeaponDrawn = true;
            InterruptCurrentWeaponAnimationIfNeeded();
            RefreshWeaponInHand();
        }

        private void OnMouseDown(MouseDown message)
        {
            if (message.Button != MouseButtonType.Left)
            {
                return;
            }

            LogLeftClick("received");

            if (gameModesController.GameMode != GameMode.Game)
            {
                LogLeftClick("ignored: not in Game mode");
                return;
            }

            if (isWeaponAnimationInProgress)
            {
                LogLeftClick("ignored: weapon animation in progress");
                return;
            }

            if (isHoldAttackActive)
            {
                LogLeftClick("ignored: hold attack already active");
                return;
            }

            if (isHitAttackInProgress)
            {
                LogLeftClick("ignored: hit attack already in progress");
                return;
            }

            var selectedItemConfig = ResolveActiveWeaponSelection();
            if (selectedItemConfig == null)
            {
                LogLeftClick("ignored: no weapon in active slot");
                return;
            }

            if (!isWeaponDrawn)
            {
                LogLeftClick("weapon on belt -> request draw", selectedItemConfig);
                isWeaponDrawn = true;
                RefreshWeaponInHand();
                return;
            }

            LogLeftClick("weapon in hand -> request HoldAttack", selectedItemConfig);
            StartHoldAttack();
        }

        private void OnMouseUp(MouseUp message)
        {
            if (message.Button != MouseButtonType.Left)
            {
                return;
            }

            LogLeftClick("release received");

            if (!isHoldAttackActive)
            {
                LogLeftClick("release ignored: no active HoldAttack");
                return;
            }

            if (isHoldAttackReady)
            {
                LogLeftClick("hold ready on release -> request Attack");
                TriggerAttack();
                return;
            }

            isAttackReleaseQueued = true;
            LogLeftClick("release queued until HoldAttackReady");
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            if (message.GameMode == GameMode.Game)
            {
                if (!isHitAttackInProgress)
                {
                    playerAnimationController?.SetLocomotionLocked(false);
                }

                return;
            }

            CancelAttackFlow();
        }

        private void RefreshWeaponInHand()
        {
            var selectedItemConfig = ResolveActiveWeaponSelection();
            var slotItemChanged = lastObservedSelectedSlotItemConfig != selectedItemConfig;

            if (slotItemChanged && !isWeaponDrawn && selectedItemConfig != null)
            {
                isWeaponDrawn = true;
            }

            lastObservedSelectedSlotItemConfig = selectedItemConfig;

            if (isWeaponAnimationInProgress)
            {
                hasPendingRefresh = true;
                return;
            }

            if (selectedItemConfig == null)
            {
                CancelAttackFlow();
                HandleNoSelectedWeapon();
                return;
            }

            if (isWeaponDrawn)
            {
                HandleDrawnWeapon(selectedItemConfig);
                return;
            }

            CancelAttackFlow();
            HandleHolsteredWeapon(selectedItemConfig);
        }

        private void HandleNoSelectedWeapon()
        {
            if (currentDisplayMode == WeaponDisplayMode.RightHand && currentWeaponItemConfig != null)
            {
                StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig);
                return;
            }

            RenderWeapon(null, 0, WeaponDisplayMode.None);
        }

        private void HandleDrawnWeapon(ItemConfig selectedItemConfig)
        {
            if (currentDisplayMode == WeaponDisplayMode.RightHand)
            {
                if (currentRenderedSlotIndex == selectedWeaponSlotIndex)
                {
                    if (currentWeaponItemConfig == selectedItemConfig)
                    {
                        return;
                    }

                    RenderWeapon(selectedItemConfig, selectedWeaponSlotIndex, WeaponDisplayMode.RightHand);
                    return;
                }

                StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig);
                return;
            }

            if (currentDisplayMode == WeaponDisplayMode.Belt
             && currentRenderedSlotIndex == selectedWeaponSlotIndex
             && currentWeaponItemConfig == selectedItemConfig)
            {
                StartDrawAnimation(selectedWeaponSlotIndex, selectedItemConfig);
                return;
            }

            RenderWeapon(selectedItemConfig, selectedWeaponSlotIndex, WeaponDisplayMode.Belt);
            StartDrawAnimation(selectedWeaponSlotIndex, selectedItemConfig);
        }

        private void HandleHolsteredWeapon(ItemConfig selectedItemConfig)
        {
            if (currentDisplayMode == WeaponDisplayMode.RightHand
             && currentRenderedSlotIndex == selectedWeaponSlotIndex
             && currentWeaponItemConfig == selectedItemConfig)
            {
                StartSheatheAnimation(selectedWeaponSlotIndex, selectedItemConfig);
                return;
            }

            if (currentDisplayMode == WeaponDisplayMode.RightHand)
            {
                StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig);
                return;
            }

            if (currentDisplayMode == WeaponDisplayMode.Belt
             && currentRenderedSlotIndex == selectedWeaponSlotIndex
             && currentWeaponItemConfig == selectedItemConfig)
            {
                return;
            }

            RenderWeapon(selectedItemConfig, selectedWeaponSlotIndex, WeaponDisplayMode.Belt);
        }

        private void StartDrawAnimation(int slotIndex, ItemConfig itemConfig)
        {
            if (itemConfig == null || isHoldAttackActive || isHitAttackInProgress)
            {
                return;
            }

            if (currentDisplayMode != WeaponDisplayMode.Belt
             || currentRenderedSlotIndex != slotIndex
             || currentWeaponItemConfig != itemConfig)
            {
                RenderWeapon(itemConfig, slotIndex, WeaponDisplayMode.Belt);
            }

            pendingAnimationTransferMode = WeaponDisplayMode.RightHand;
            pendingAnimationTransferAction = () =>
            {
                if (selectedWeaponSlotIndex == slotIndex
                 && isWeaponDrawn
                 && GetSelectedWeaponItemConfig() == itemConfig)
                {
                    FinalizeWeaponRender(itemConfig, slotIndex, WeaponDisplayMode.RightHand);
                }
            };

            isWeaponAnimationInProgress = true;
            hasPendingRefresh = false;
            currentAnimationKind = WeaponAnimationKind.Draw;
            currentAnimationClipName = DrawWeaponClipName;

            ResetAnimatorRequests();
            animator?.SetTrigger(DrawWeaponRequestedParameterHash);
        }

        private void StartSheatheAnimation(int slotIndex, ItemConfig itemConfig)
        {
            if (itemConfig == null)
            {
                RenderWeapon(null, 0, WeaponDisplayMode.None);
                return;
            }

            if (isHoldAttackActive || isHitAttackInProgress)
            {
                return;
            }

            pendingAnimationTransferMode = WeaponDisplayMode.Belt;
            pendingAnimationTransferAction = () => FinalizeWeaponRender(itemConfig, slotIndex, WeaponDisplayMode.Belt);

            isWeaponAnimationInProgress = true;
            hasPendingRefresh = false;
            currentAnimationKind = WeaponAnimationKind.Sheathe;
            currentAnimationClipName = SheatheWeaponClipName;

            ResetAnimatorRequests();
            animator?.SetTrigger(SheatheWeaponRequestedParameterHash);
        }

        private void StartHoldAttack()
        {
            if (animator == null)
            {
                LogLeftClick("HoldAttack skipped: animator missing");
                return;
            }

            isHoldAttackActive = true;
            isHoldAttackReady = false;
            isAttackReleaseQueued = false;

            animator.ResetTrigger(HoldAttackRequestedParameterHash);
            animator.SetTrigger(HoldAttackRequestedParameterHash);
            LogLeftClick("HoldAttack trigger sent");
        }

        private void TriggerAttack()
        {
            if (animator == null)
            {
                LogLeftClick("Attack skipped: animator missing");
                return;
            }

            isAttackReleaseQueued = false;
            isHoldAttackActive = false;
            isHoldAttackReady = false;

            animator.ResetTrigger(AttackRequestedParameterHash);
            animator.SetTrigger(AttackRequestedParameterHash);
            LogLeftClick("Attack trigger sent");
        }

        private void TryConsumeQueuedAttackRelease()
        {
            if (!isAttackReleaseQueued || !isHoldAttackReady)
            {
                return;
            }

            TriggerAttack();
        }

        private void CancelAttackFlow()
        {
            var hadActiveAttackFlow =
                isHoldAttackActive
             || isHoldAttackReady
             || isAttackReleaseQueued
             || isHitAttackInProgress;

            isHoldAttackActive = false;
            isHoldAttackReady = false;
            isAttackReleaseQueued = false;

            if (isHitAttackInProgress)
            {
                isHitAttackInProgress = false;
                if (gameModesController.GameMode == GameMode.Game)
                {
                    playerMovement?.ChangeState(true);
                }

                playerAnimationController?.SetLocomotionLocked(false);
            }

            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(HoldAttackRequestedParameterHash);
            animator.ResetTrigger(AttackRequestedParameterHash);

            if (hadActiveAttackFlow)
            {
                LogLeftClick("attack flow cancelled");
            }
        }

        private void CompleteWeaponAnimationFromEvent(WeaponAnimationKind expectedAnimationKind)
        {
            if (!isWeaponAnimationInProgress || currentAnimationKind != expectedAnimationKind)
            {
                return;
            }

            isWeaponAnimationInProgress = false;
            currentAnimationKind = WeaponAnimationKind.None;
            currentAnimationClipName = null;
            ResetAnimatorRequests();

            if (!hasPendingRefresh)
            {
                return;
            }

            hasPendingRefresh = false;
            RefreshWeaponInHand();
        }

        private void ApplyPendingAnimationTransfer(WeaponDisplayMode displayMode)
        {
            if (pendingAnimationTransferMode != displayMode || pendingAnimationTransferAction == null)
            {
                return;
            }

            pendingAnimationTransferAction.Invoke();
            pendingAnimationTransferMode = WeaponDisplayMode.None;
            pendingAnimationTransferAction = null;
        }

        private void InterruptCurrentWeaponAnimationIfNeeded()
        {
            if (!isWeaponAnimationInProgress)
            {
                return;
            }

            weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
            pendingAnimationTransferMode = WeaponDisplayMode.None;
            pendingAnimationTransferAction = null;
            isWeaponAnimationInProgress = false;
            hasPendingRefresh = false;
            currentAnimationKind = WeaponAnimationKind.None;
            currentAnimationClipName = null;
            ResetAnimatorRequests();
        }

        private ItemConfig GetSelectedWeaponItemConfig()
        {
            var selectedSlot = selectedWeaponSlotIndex == 1
                ? playerInventory.LeftWeaponSlot
                : playerInventory.RightWeaponSlot;
            var itemConfig = selectedSlot?.ItemConfig;

            return itemConfig?.ItemType == ItemType.Weapon
                ? itemConfig
                : null;
        }

        private ItemConfig ResolveActiveWeaponSelection()
        {
            var selectedItemConfig = GetSelectedWeaponItemConfig();
            if (selectedItemConfig != null)
            {
                return selectedItemConfig;
            }

            var leftWeaponItemConfig = GetWeaponItemConfigForSlot(1);
            var rightWeaponItemConfig = GetWeaponItemConfigForSlot(2);

            if (leftWeaponItemConfig != null && rightWeaponItemConfig == null)
            {
                selectedWeaponSlotIndex = 1;
                return leftWeaponItemConfig;
            }

            if (rightWeaponItemConfig != null && leftWeaponItemConfig == null)
            {
                selectedWeaponSlotIndex = 2;
                return rightWeaponItemConfig;
            }

            return null;
        }

        private ItemConfig GetWeaponItemConfigForSlot(int slotIndex)
        {
            var slot = slotIndex == 1
                ? playerInventory.LeftWeaponSlot
                : playerInventory.RightWeaponSlot;
            var itemConfig = slot?.ItemConfig;

            return itemConfig?.ItemType == ItemType.Weapon
                ? itemConfig
                : null;
        }

        private void RenderWeapon(ItemConfig itemConfig, int slotIndex, WeaponDisplayMode displayMode)
        {
            DestroyCurrentWeaponInstance();

            if (itemConfig == null || displayMode == WeaponDisplayMode.None)
            {
                currentWeaponItemConfig = null;
                currentRenderedSlotIndex = 0;
                currentDisplayMode = WeaponDisplayMode.None;
                return;
            }

            var weaponPrefab = itemConfig.ItemType == ItemType.Weapon
                ? itemConfig.WeaponInHandPrefab
                : null;
            var targetParent = GetTargetParent(displayMode);
            if (weaponPrefab == null || targetParent == null)
            {
                currentWeaponItemConfig = null;
                currentRenderedSlotIndex = 0;
                currentDisplayMode = WeaponDisplayMode.None;
                return;
            }

            currentWeaponItemConfig = itemConfig;
            currentRenderedSlotIndex = slotIndex;
            currentDisplayMode = displayMode;

            currentWeaponInstance = Object.Instantiate(weaponPrefab, targetParent, false);
            currentWeaponInstance.name = $"{weaponPrefab.name} | {displayMode}";
            ApplyAttachmentTransform(currentWeaponInstance.transform, itemConfig, displayMode);
        }

        private void FinalizeWeaponRender(ItemConfig itemConfig, int slotIndex, WeaponDisplayMode displayMode)
        {
            var targetParent = GetTargetParent(displayMode);
            if (currentWeaponInstance != null
             && currentWeaponItemConfig == itemConfig
             && targetParent != null)
            {
                currentWeaponInstance.transform.SetParent(targetParent, false);
                ApplyAttachmentTransform(currentWeaponInstance.transform, itemConfig, displayMode);
                currentRenderedSlotIndex = slotIndex;
                currentDisplayMode = displayMode;
                return;
            }

            RenderWeapon(itemConfig, slotIndex, displayMode);
        }

        private void DestroyCurrentWeaponInstance()
        {
            if (currentWeaponInstance == null)
            {
                return;
            }

            Object.Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
        }

        private Transform GetTargetParent(WeaponDisplayMode displayMode)
        {
            return displayMode switch
            {
                WeaponDisplayMode.RightHand => handAnchor?.RightHand,
                WeaponDisplayMode.Belt => handAnchor?.Belt,
                _ => null
            };
        }

        private void StartWeaponAttachmentBlend(
            WeaponDisplayMode targetMode,
            string startEventName,
            string finishEventName)
        {
            if (currentWeaponInstance == null || currentWeaponItemConfig == null)
            {
                return;
            }

            var targetParent = GetTargetParent(targetMode);
            var attachment = GetAttachmentTransformData(currentWeaponItemConfig, targetMode);
            if (targetParent == null || attachment == null)
            {
                return;
            }

            var weaponTransform = currentWeaponInstance.transform;
            weaponTransform.SetParent(targetParent, true);
            currentDisplayMode = targetMode;

            if (TryGetAnimationEventWindowNormalized(
                    currentAnimationClipName,
                    startEventName,
                    finishEventName,
                    out var startNormalizedTime,
                    out var finishNormalizedTime))
            {
                StartTransformBlendByNormalizedTime(
                    weaponTransform,
                    attachment.LocalPosition,
                    Quaternion.Euler(attachment.LocalEulerAngles),
                    attachment.LocalScale,
                    startNormalizedTime,
                    finishNormalizedTime);
                return;
            }

            StartTransformBlendByDuration(
                weaponTransform,
                attachment.LocalPosition,
                Quaternion.Euler(attachment.LocalEulerAngles),
                attachment.LocalScale,
                FallbackAttachmentBlendDuration);
        }

        private void StartTransformBlendByDuration(
            Transform targetTransform,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            Vector3 targetLocalScale,
            float duration)
        {
            weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;

            if (targetTransform == null)
            {
                return;
            }

            if (duration <= 0f)
            {
                targetTransform.localPosition = targetLocalPosition;
                targetTransform.localRotation = targetLocalRotation;
                targetTransform.localScale = targetLocalScale;
                return;
            }

            var startLocalPosition = targetTransform.localPosition;
            var startLocalRotation = targetTransform.localRotation;
            var startLocalScale = targetTransform.localScale;
            var elapsed = 0f;

            weaponAttachmentBlendDisposable.Disposable = Observable.EveryUpdate()
                .ObserveOnMainThread()
                .Subscribe(_ =>
                {
                    if (targetTransform == null)
                    {
                        weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                        return;
                    }

                    elapsed += Time.deltaTime;
                    var progress = Mathf.Clamp01(elapsed / duration);

                    targetTransform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, progress);
                    targetTransform.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, progress);
                    targetTransform.localScale = Vector3.Lerp(startLocalScale, targetLocalScale, progress);

                    if (progress < 1f)
                    {
                        return;
                    }

                    weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                });
        }

        private void StartTransformBlendByNormalizedTime(
            Transform targetTransform,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            Vector3 targetLocalScale,
            float startNormalizedTime,
            float finishNormalizedTime)
        {
            weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;

            if (targetTransform == null)
            {
                return;
            }

            var startLocalPosition = targetTransform.localPosition;
            var startLocalRotation = targetTransform.localRotation;
            var startLocalScale = targetTransform.localScale;
            var targetStateHash = GetCurrentAnimationStateHash();

            weaponAttachmentBlendDisposable.Disposable = Observable.EveryUpdate()
                .ObserveOnMainThread()
                .Subscribe(_ =>
                {
                    if (targetTransform == null || animator == null || weaponAnimationLayerIndex < 0)
                    {
                        weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                        return;
                    }

                    var stateInfo = animator.GetCurrentAnimatorStateInfo(weaponAnimationLayerIndex);
                    if (stateInfo.fullPathHash != targetStateHash)
                    {
                        weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                        return;
                    }

                    var progress = Mathf.InverseLerp(startNormalizedTime, finishNormalizedTime, stateInfo.normalizedTime);
                    targetTransform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, progress);
                    targetTransform.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, progress);
                    targetTransform.localScale = Vector3.Lerp(startLocalScale, targetLocalScale, progress);

                    if (progress < 1f)
                    {
                        return;
                    }

                    weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                });
        }

        private bool TryGetAnimationEventWindowNormalized(
            string clipName,
            string startEventName,
            string finishEventName,
            out float startNormalizedTime,
            out float finishNormalizedTime)
        {
            var clip = GetAnimationClip(clipName);
            if (clip == null)
            {
                startNormalizedTime = 0f;
                finishNormalizedTime = 0f;
                return false;
            }

            float? startTime = null;
            float? finishTime = null;

            foreach (var animationEvent in clip.events)
            {
                if (!startTime.HasValue && animationEvent.functionName == startEventName)
                {
                    startTime = animationEvent.time;
                }

                if (!finishTime.HasValue && animationEvent.functionName == finishEventName)
                {
                    finishTime = animationEvent.time;
                }
            }

            if (!startTime.HasValue || !finishTime.HasValue || finishTime.Value <= startTime.Value || clip.length <= 0f)
            {
                startNormalizedTime = 0f;
                finishNormalizedTime = 0f;
                return false;
            }

            startNormalizedTime = startTime.Value / clip.length;
            finishNormalizedTime = finishTime.Value / clip.length;
            return true;
        }

        private int GetCurrentAnimationStateHash()
        {
            return currentAnimationKind switch
            {
                WeaponAnimationKind.Draw => DrawWeaponStateHash,
                WeaponAnimationKind.Sheathe => SheatheWeaponStateHash,
                _ => 0
            };
        }

        private AnimationClip GetAnimationClip(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName) || animator?.runtimeAnimatorController == null)
            {
                return null;
            }

            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == clipName)
                {
                    return clip;
                }
            }

            return null;
        }

        private void ResetAnimatorRequests()
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(DrawWeaponRequestedParameterHash);
            animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
            animator.ResetTrigger(HoldAttackRequestedParameterHash);
            animator.ResetTrigger(AttackRequestedParameterHash);
        }

        private bool TryGetHoldAttackWindUpStateInfo(out AnimatorStateInfo stateInfo, out int layerIndex)
        {
            stateInfo = default;
            layerIndex = -1;

            if (animator == null)
            {
                return false;
            }

            for (var index = 0; index < animator.layerCount; index++)
            {
                var currentStateInfo = animator.GetCurrentAnimatorStateInfo(index);
                if (currentStateInfo.shortNameHash != HoldAttackWindUpStateShortNameHash)
                {
                    continue;
                }

                stateInfo = currentStateInfo;
                layerIndex = index;
                return true;
            }

            return false;
        }

        private void LogLeftClick(string message, ItemConfig selectedItemConfig = null)
        {
            Debug.Log(
                $"{LeftClickLogPrefix} {message} | " +
                $"selectedSlot={selectedWeaponSlotIndex} " +
                $"selectedItem={(selectedItemConfig ?? GetSelectedWeaponItemConfig())?.name ?? "null"} " +
                $"isWeaponDrawn={isWeaponDrawn} " +
                $"currentMode={currentDisplayMode} " +
                $"currentItem={currentWeaponItemConfig?.name ?? "null"} " +
                $"weaponAnimInProgress={isWeaponAnimationInProgress} " +
                $"holdActive={isHoldAttackActive} " +
                $"holdReady={isHoldAttackReady} " +
                $"releaseQueued={isAttackReleaseQueued} " +
                $"hitInProgress={isHitAttackInProgress} " +
                $"gameMode={gameModesController.GameMode}");
        }

        private static WeaponAttachmentTransformData GetAttachmentTransformData(ItemConfig itemConfig, WeaponDisplayMode displayMode)
        {
            if (itemConfig == null)
            {
                return null;
            }

            return displayMode == WeaponDisplayMode.RightHand
                ? itemConfig.RightHandWeaponAttachment
                : itemConfig.BeltWeaponAttachment;
        }

        private static void ApplyAttachmentTransform(Transform targetTransform, ItemConfig itemConfig, WeaponDisplayMode displayMode)
        {
            if (targetTransform == null || itemConfig == null)
            {
                return;
            }

            var attachment = GetAttachmentTransformData(itemConfig, displayMode);
            if (attachment == null)
            {
                return;
            }

            targetTransform.localPosition = attachment.LocalPosition;
            targetTransform.localRotation = Quaternion.Euler(attachment.LocalEulerAngles);
            targetTransform.localScale = attachment.LocalScale;
        }
    }
}
