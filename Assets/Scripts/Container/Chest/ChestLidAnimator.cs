using System;
using DG.Tweening;
using UnityEngine;

namespace Container.Chest
{
    [Serializable]
    public sealed class ChestLidAnimationSettings
    {
        [field: SerializeField] public Transform Lid { get; private set; }
        [field: SerializeField, Min(0f)] public float OpenDuration { get; private set; } = 0.3f;
        [field: SerializeField, Min(0f)] public float CloseDuration { get; private set; } = 0.25f;
        [field: SerializeField] public Ease Ease { get; private set; } = Ease.OutQuad;
        [field: SerializeField] public Vector3 OpenLocalRotationOffset { get; private set; } = new(-110f, 0f, 0f);
    }

    public sealed class ChestLidAnimator : IDisposable
    {
        private readonly ChestLidAnimationSettings settings;
        private readonly Quaternion closedLocalRotation;
        private Tween rotationTween;

        public ChestLidAnimator(ChestLidAnimationSettings settings)
        {
            this.settings = settings;
            closedLocalRotation = settings?.Lid != null ? settings.Lid.localRotation : Quaternion.identity;
        }

        public void Open() => AnimateTo(closedLocalRotation * Quaternion.Euler(settings.OpenLocalRotationOffset), settings.OpenDuration);
        public void Close() => AnimateTo(closedLocalRotation, settings.CloseDuration);
        public void Dispose() => rotationTween?.Kill();

        private void AnimateTo(Quaternion targetRotation, float duration)
        {
            if (settings?.Lid == null) return;
            rotationTween?.Kill();
            rotationTween = settings.Lid.DOLocalRotateQuaternion(targetRotation, duration).SetEase(settings.Ease);
        }
    }
}
