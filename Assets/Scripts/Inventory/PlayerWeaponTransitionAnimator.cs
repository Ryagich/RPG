using UnityEngine;

namespace Inventory
{
    internal sealed class PlayerWeaponTransitionAnimator
    {
        private const string WeaponLayerName = "Weapon Handling";
        private const string DrawClipName = "A_Draw_Sword";
        private const string SheatheClipName = "A_Sheathe_Sword";
        private static readonly int DrawTriggerHash = Animator.StringToHash("MoveWeaponInHand");
        private static readonly int SheatheTriggerHash = Animator.StringToHash("MoveWeaponInBelt");
        private static readonly int DrawStateHash = Animator.StringToHash("Weapon Handling.DrawWeapon");
        private static readonly int SheatheStateHash = Animator.StringToHash("Weapon Handling.SheatheWeapon");

        private readonly Animator animator;

        public PlayerWeaponTransitionAnimator(Animator animator)
        {
            this.animator = animator;
            LayerIndex = animator != null ? animator.GetLayerIndex(WeaponLayerName) : -1;
        }

        public int LayerIndex { get; }
        public bool IsAvailable => animator != null && LayerIndex >= 0;

        public void Request(WeaponAnimationKind kind)
        {
            if (animator == null || kind == WeaponAnimationKind.None)
            {
                return;
            }

            animator.ResetTrigger(DrawTriggerHash);
            animator.ResetTrigger(SheatheTriggerHash);
            animator.SetTrigger(kind == WeaponAnimationKind.Draw ? DrawTriggerHash : SheatheTriggerHash);
        }

        public void ResetRequests()
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(DrawTriggerHash);
            animator.ResetTrigger(SheatheTriggerHash);
        }

        public bool IsStateActive(WeaponAnimationKind kind)
        {
            if (!IsAvailable || kind == WeaponAnimationKind.None)
            {
                return false;
            }

            var stateHash = GetStateHash(kind);
            if (animator.GetCurrentAnimatorStateInfo(LayerIndex).fullPathHash == stateHash)
            {
                return true;
            }

            return animator.IsInTransition(LayerIndex)
                   && animator.GetNextAnimatorStateInfo(LayerIndex).fullPathHash == stateHash;
        }

        public bool IsStateExpectedForAnimationEvent(WeaponAnimationKind kind)
        {
            if (!IsAvailable || kind == WeaponAnimationKind.None)
            {
                return false;
            }

            var stateHash = GetStateHash(kind);
            if (animator.IsInTransition(LayerIndex))
            {
                return animator.GetNextAnimatorStateInfo(LayerIndex).fullPathHash == stateHash;
            }

            return animator.GetCurrentAnimatorStateInfo(LayerIndex).fullPathHash == stateHash;
        }

        public int GetStateHash(WeaponAnimationKind kind)
        {
            return kind switch
            {
                WeaponAnimationKind.Draw => DrawStateHash,
                WeaponAnimationKind.Sheathe => SheatheStateHash,
                _ => 0
            };
        }

        public AnimationClip GetClip(WeaponAnimationKind kind)
        {
            if (animator?.runtimeAnimatorController == null)
            {
                return null;
            }

            var expectedClipName = kind switch
            {
                WeaponAnimationKind.Draw => DrawClipName,
                WeaponAnimationKind.Sheathe => SheatheClipName,
                _ => null
            };
            if (expectedClipName == null)
            {
                return null;
            }
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == expectedClipName)
                {
                    return clip;
                }
            }

            return null;
        }

        public bool TryGetEventNormalizedTime(
            WeaponAnimationKind kind,
            string eventName,
            out float normalizedTime)
        {
            normalizedTime = 0f;
            var clip = GetClip(kind);
            if (clip == null || clip.length <= 0f)
            {
                return false;
            }

            foreach (var animationEvent in clip.events)
            {
                if (animationEvent.functionName != eventName)
                {
                    continue;
                }

                normalizedTime = animationEvent.time / clip.length;
                return true;
            }

            return false;
        }

        public bool HasReachedEvent(WeaponAnimationKind kind, string eventName)
        {
            if (!IsAvailable
                || !TryGetEventNormalizedTime(kind, eventName, out var normalizedEventTime))
            {
                return false;
            }

            var stateInfo = animator.GetCurrentAnimatorStateInfo(LayerIndex);
            return stateInfo.fullPathHash == GetStateHash(kind)
                   && stateInfo.normalizedTime >= normalizedEventTime;
        }
    }
}
