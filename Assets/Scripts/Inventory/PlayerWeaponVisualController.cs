using System;
using Inventory.Item;
using UniRx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Inventory
{
    internal sealed class PlayerWeaponVisualController : IDisposable
    {
        private const float FallbackAttachmentBlendDuration = 0.08f;

        private readonly PlayerWeaponHandAnchor handAnchor;
        private readonly Animator animator;
        private readonly int weaponAnimationLayerIndex;
        private readonly SerialDisposable attachmentBlendDisposable = new();

        public PlayerWeaponVisualController(
            PlayerWeaponHandAnchor handAnchor,
            Animator animator,
            int weaponAnimationLayerIndex)
        {
            this.handAnchor = handAnchor;
            this.animator = animator;
            this.weaponAnimationLayerIndex = weaponAnimationLayerIndex;
        }

        public GameObject Instance { get; private set; }
        public ItemConfig ItemConfig { get; private set; }
        public int SlotIndex { get; private set; }
        public WeaponDisplayMode DisplayMode { get; private set; }

        public bool IsAttachedTo(WeaponDisplayMode displayMode)
        {
            return Instance != null && Instance.transform.parent == GetTargetParent(displayMode);
        }

        public bool TryGetPose(out Vector3 position, out Quaternion rotation)
        {
            if (Instance != null)
            {
                position = Instance.transform.position;
                rotation = Instance.transform.rotation;
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }

        public void Render(ItemConfig itemConfig, int slotIndex, WeaponDisplayMode displayMode)
        {
            Destroy();
            CleanupExceptCurrent();

            if (itemConfig == null || displayMode == WeaponDisplayMode.None)
            {
                ClearState();
                return;
            }

            var weaponPrefab = itemConfig.ItemType == ItemType.Weapon
                ? itemConfig.WeaponInHandPrefab
                : null;
            var targetParent = GetTargetParent(displayMode);
            if (weaponPrefab == null || targetParent == null)
            {
                ClearState();
                return;
            }

            ItemConfig = itemConfig;
            SlotIndex = slotIndex;
            DisplayMode = displayMode;
            Instance = Object.Instantiate(weaponPrefab, targetParent, false);
            if (Instance.GetComponent<PlayerWeaponVisualInstance>() == null)
            {
                Instance.AddComponent<PlayerWeaponVisualInstance>();
            }
            UpdateInstanceName();
            ApplyAttachmentTransform(Instance.transform, itemConfig, displayMode);
        }

        public void FinalizeRender(
            ItemConfig itemConfig,
            int slotIndex,
            WeaponDisplayMode displayMode,
            bool snapToAttachmentTransform)
        {
            var targetParent = GetTargetParent(displayMode);
            if (Instance != null && ItemConfig == itemConfig && targetParent != null)
            {
                Instance.transform.SetParent(targetParent, !snapToAttachmentTransform);
                if (snapToAttachmentTransform)
                {
                    ApplyAttachmentTransform(Instance.transform, itemConfig, displayMode);
                }

                SlotIndex = slotIndex;
                DisplayMode = displayMode;
                UpdateInstanceName();
                return;
            }

            Render(itemConfig, slotIndex, displayMode);
        }

        public void MovePreservingPose(WeaponDisplayMode displayMode)
        {
            if (Instance == null || ItemConfig == null)
            {
                return;
            }

            var targetParent = GetTargetParent(displayMode);
            if (targetParent == null)
            {
                return;
            }

            attachmentBlendDisposable.Disposable = Disposable.Empty;
            Instance.transform.SetParent(targetParent, true);
            DisplayMode = displayMode;
            UpdateInstanceName();
        }

        public void StartAttachmentBlend(
            WeaponDisplayMode targetMode,
            AnimationClip animationClip,
            int animationStateHash,
            string startEventName,
            string finishEventName)
        {
            if (Instance == null || ItemConfig == null)
            {
                return;
            }

            var targetParent = GetTargetParent(targetMode);
            var attachment = GetAttachmentTransformData(ItemConfig, targetMode);
            if (targetParent == null || attachment == null)
            {
                return;
            }

            var weaponTransform = Instance.transform;
            weaponTransform.SetParent(targetParent, true);
            DisplayMode = targetMode;
            UpdateInstanceName();

            if (targetMode == WeaponDisplayMode.Belt)
            {
                return;
            }

            if (TryGetAnimationEventWindowNormalized(
                    animationClip,
                    startEventName,
                    finishEventName,
                    out var startNormalizedTime,
                    out var finishNormalizedTime))
            {
                StartTransformBlendByNormalizedTime(
                    weaponTransform,
                    attachment.LocalPosition,
                    Quaternion.Euler(attachment.LocalEulerAngles),
                    animationStateHash,
                    startNormalizedTime,
                    finishNormalizedTime);
                return;
            }

            StartTransformBlendByDuration(
                weaponTransform,
                attachment.LocalPosition,
                Quaternion.Euler(attachment.LocalEulerAngles),
                FallbackAttachmentBlendDuration);
        }

        public void CleanupExceptCurrent()
        {
            CleanupAnchor(handAnchor?.RightHand);
            CleanupAnchor(handAnchor?.Belt);
        }

        public void Destroy()
        {
            attachmentBlendDisposable.Disposable = Disposable.Empty;
            if (Instance != null)
            {
                Object.Destroy(Instance);
            }

            Instance = null;
            ClearState();
        }

        public void Dispose()
        {
            attachmentBlendDisposable.Dispose();
            Destroy();
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

        private void ClearState()
        {
            ItemConfig = null;
            SlotIndex = 0;
            DisplayMode = WeaponDisplayMode.None;
        }

        private void CleanupAnchor(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            for (var index = anchor.childCount - 1; index >= 0; index--)
            {
                var child = anchor.GetChild(index);
                if (child == null
                 || child.gameObject == Instance
                 || child.GetComponent<PlayerWeaponVisualInstance>() == null)
                {
                    continue;
                }

                Object.Destroy(child.gameObject);
            }
        }

        private void UpdateInstanceName()
        {
            if (Instance != null && ItemConfig?.WeaponInHandPrefab != null)
            {
                Instance.name = $"{ItemConfig.WeaponInHandPrefab.name} | {DisplayMode}";
            }
        }

        private void StartTransformBlendByDuration(Transform targetTransform, Vector3 targetLocalPosition, Quaternion targetLocalRotation, float duration)
        {
            attachmentBlendDisposable.Disposable = Disposable.Empty;
            if (targetTransform == null)
            {
                return;
            }

            if (duration <= 0f)
            {
                targetTransform.localPosition = targetLocalPosition;
                targetTransform.localRotation = targetLocalRotation;
                return;
            }

            var startLocalPosition = targetTransform.localPosition;
            var startLocalRotation = targetTransform.localRotation;
            var elapsed = 0f;
            attachmentBlendDisposable.Disposable = Observable.EveryUpdate().ObserveOnMainThread().Subscribe(_ =>
            {
                if (targetTransform == null)
                {
                    attachmentBlendDisposable.Disposable = Disposable.Empty;
                    return;
                }

                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                targetTransform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, progress);
                targetTransform.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, progress);
                if (progress >= 1f)
                {
                    attachmentBlendDisposable.Disposable = Disposable.Empty;
                }
            });
        }

        private void StartTransformBlendByNormalizedTime(Transform targetTransform, Vector3 targetLocalPosition, Quaternion targetLocalRotation, int animationStateHash, float startNormalizedTime, float finishNormalizedTime)
        {
            attachmentBlendDisposable.Disposable = Disposable.Empty;
            if (targetTransform == null)
            {
                return;
            }

            var startLocalPosition = targetTransform.localPosition;
            var startLocalRotation = targetTransform.localRotation;
            attachmentBlendDisposable.Disposable = Observable.EveryUpdate().ObserveOnMainThread().Subscribe(_ =>
            {
                if (targetTransform == null || animator == null || weaponAnimationLayerIndex < 0)
                {
                    attachmentBlendDisposable.Disposable = Disposable.Empty;
                    return;
                }

                var stateInfo = animator.GetCurrentAnimatorStateInfo(weaponAnimationLayerIndex);
                if (stateInfo.fullPathHash != animationStateHash)
                {
                    attachmentBlendDisposable.Disposable = Disposable.Empty;
                    return;
                }

                var progress = Mathf.InverseLerp(startNormalizedTime, finishNormalizedTime, stateInfo.normalizedTime);
                targetTransform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, progress);
                targetTransform.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, progress);
                if (progress >= 1f)
                {
                    attachmentBlendDisposable.Disposable = Disposable.Empty;
                }
            });
        }

        private static bool TryGetAnimationEventWindowNormalized(AnimationClip clip, string startEventName, string finishEventName, out float startNormalizedTime, out float finishNormalizedTime)
        {
            startNormalizedTime = 0f;
            finishNormalizedTime = 0f;
            if (clip == null || clip.length <= 0f)
            {
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

            if (!startTime.HasValue || !finishTime.HasValue || finishTime.Value <= startTime.Value)
            {
                return false;
            }

            startNormalizedTime = startTime.Value / clip.length;
            finishNormalizedTime = finishTime.Value / clip.length;
            return true;
        }

        private static WeaponAttachmentTransformData GetAttachmentTransformData(ItemConfig itemConfig, WeaponDisplayMode displayMode)
        {
            return displayMode == WeaponDisplayMode.RightHand
                ? itemConfig?.RightHandWeaponAttachment
                : itemConfig?.BeltWeaponAttachment;
        }

        private static void ApplyAttachmentTransform(Transform targetTransform, ItemConfig itemConfig, WeaponDisplayMode displayMode)
        {
            var attachment = GetAttachmentTransformData(itemConfig, displayMode);
            if (targetTransform == null || attachment == null)
            {
                return;
            }

            targetTransform.localPosition = attachment.LocalPosition;
            targetTransform.localRotation = Quaternion.Euler(attachment.LocalEulerAngles);
        }
    }
}
