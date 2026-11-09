using Combat;
using UnityEngine;

namespace CharacterAnimation
{
    /// <summary>
    /// Receives character-control events authored in Animator clips and forwards them to the
    /// runtime owner for this character. The receiver contains no player- or NPC-specific logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationEventReceiver : MonoBehaviour
    {
        private ICharacterAnimationControlEventHandler controlEventHandler;
        private CharacterRootMotionController rootMotionController;
        private string controlLockOwnerAnimationName;
        private string rootMotionOwnerAnimationName;

        public void BindRootMotion(CharacterRootMotionController rootMotionController)
        {
            this.rootMotionController = rootMotionController;
        }

        public void UnbindRootMotion(CharacterRootMotionController rootMotionController)
        {
            if (ReferenceEquals(this.rootMotionController, rootMotionController))
            {
                this.rootMotionController = null;
            }
        }

        public void Bind(ICharacterAnimationControlEventHandler handler)
        {
            controlEventHandler = handler;
        }

        public void Unbind(ICharacterAnimationControlEventHandler handler)
        {
            if (ReferenceEquals(controlEventHandler, handler))
            {
                controlEventHandler = null;
            }
        }

        // These identifiers are part of the serialized Animation Event contract used by the shared
        // Full Body and Air layers. Every directional clip in one action Blend Tree must use its
        // shared action identifier: changing the blend direction must not change lock ownership.
        public void LockMovement(string animationName)
        {
            controlLockOwnerAnimationName = animationName;
            controlEventHandler?.LockCharacterControlFromAnimationEvent();
        }

        public void UnlockMovement(string animationName)
        {
            if (controlLockOwnerAnimationName != animationName)
            {
                return;
            }

            controlLockOwnerAnimationName = null;
            controlEventHandler?.UnlockCharacterControlFromAnimationEvent();
        }

        public void EnableRootMotion(AnimationEvent animationEvent)
        {
            rootMotionOwnerAnimationName = animationEvent.stringParameter;
            rootMotionController?.SetRootMotionActive(this, true, animationEvent.floatParameter);
        }

        public void DisableRootMotion(AnimationEvent animationEvent)
        {
            if (rootMotionOwnerAnimationName != animationEvent.stringParameter)
            {
                return;
            }

            rootMotionOwnerAnimationName = null;
            rootMotionController?.SetRootMotionActive(this, false);
        }

        /// <summary>Releases animation-owned control and root motion when an action is interrupted.</summary>
        public void ReleaseAnimationControl()
        {
            controlLockOwnerAnimationName = null;
            rootMotionOwnerAnimationName = null;
            controlEventHandler?.UnlockCharacterControlFromAnimationEvent();
            rootMotionController?.SetRootMotionActive(this, false);
        }
    }

    public interface ICharacterAnimationControlEventHandler
    {
        void LockCharacterControlFromAnimationEvent();
        void UnlockCharacterControlFromAnimationEvent();
    }
}
