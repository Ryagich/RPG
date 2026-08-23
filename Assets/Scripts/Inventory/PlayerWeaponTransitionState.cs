namespace Inventory
{
    internal sealed class PlayerWeaponTransitionState
    {
        public bool IsWeaponDrawn { get; private set; }
        public bool IsAnimationInProgress { get; private set; }
        public bool HasEnteredSheatheState { get; private set; }
        public bool HasAttachmentBlendStarted { get; private set; }
        public bool ShouldPreservePoseForDraw { get; private set; }
        public WeaponAnimationKind CurrentKind { get; private set; }

        public void SetWeaponDrawn(bool value)
        {
            IsWeaponDrawn = value;
        }

        public void Begin(WeaponAnimationKind kind, bool preservePoseForDraw = false)
        {
            IsAnimationInProgress = true;
            HasEnteredSheatheState = false;
            HasAttachmentBlendStarted = false;
            ShouldPreservePoseForDraw = preservePoseForDraw;
            CurrentKind = kind;
        }

        public bool TryBeginAttachmentBlend(WeaponAnimationKind expectedKind)
        {
            if (CurrentKind != expectedKind || HasAttachmentBlendStarted)
            {
                return false;
            }

            HasAttachmentBlendStarted = true;
            return true;
        }

        public void MarkSheatheStateEntered()
        {
            HasEnteredSheatheState = true;
        }

        public bool CanSynchronizeSheathe()
        {
            return IsAnimationInProgress
                   && CurrentKind == WeaponAnimationKind.Sheathe
                   && HasEnteredSheatheState;
        }

        public bool Complete(WeaponAnimationKind expectedKind)
        {
            if (!IsAnimationInProgress || CurrentKind != expectedKind)
            {
                return false;
            }

            IsAnimationInProgress = false;
            HasEnteredSheatheState = false;
            HasAttachmentBlendStarted = false;
            ShouldPreservePoseForDraw = false;
            CurrentKind = WeaponAnimationKind.None;
            return true;
        }
    }
}
