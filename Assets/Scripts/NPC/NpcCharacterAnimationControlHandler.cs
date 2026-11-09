using System;
using CharacterAnimation;
using Combat;
using VContainer.Unity;

namespace NPC
{
    /// <summary>Executes common animation-control events for an NPC character.</summary>
    public sealed class NpcCharacterAnimationControlHandler : ICharacterAnimationControlEventHandler, IStartable, IDisposable
    {
        private readonly CharacterAnimationEventReceiver receiver;
        private readonly CharacterActionState actionState;
        private readonly NpcNavMeshController navigation;

        public NpcCharacterAnimationControlHandler(
            CharacterAnimationEventReceiver receiver,
            CharacterActionState actionState,
            NpcNavMeshController navigation)
        {
            this.receiver = receiver;
            this.actionState = actionState;
            this.navigation = navigation;
        }

        public void Start()
        {
            receiver?.Bind(this);
        }

        public void Dispose()
        {
            ReleaseAnimationControl();
            receiver?.Unbind(this);
        }

        public void LockCharacterControlFromAnimationEvent()
        {
            actionState?.SetActionBlocked(this, true);
            navigation?.SetActionMovementLocked(this, true);
        }

        public void UnlockCharacterControlFromAnimationEvent()
        {
            actionState?.SetActionBlocked(this, false);
            navigation?.SetActionMovementLocked(this, false);
        }

        public void ReleaseAnimationControl()
        {
            if (receiver != null)
            {
                receiver.ReleaseAnimationControl();
                return;
            }

            UnlockCharacterControlFromAnimationEvent();
        }
    }
}
