using Combat;
using MessagePipe;
using Messages;
using UnityEngine;

namespace Monsters
{
    /// <summary>
    /// Adapts the common character hit-reaction lifecycle to PlantMonster_1's single
    /// hit animation. The shared base owns damage accumulation and cooldowns; this class
    /// owns only monster-specific animation and navigation consequences.
    /// </summary>
    public sealed class PlantMonsterHitReactionController : CharacterHitReactionControllerBase
    {
        private static readonly int GotHitState = Animator.StringToHash("GotHit");
        private readonly Animator animator;
        private bool hasEnteredHitAnimation;

        public PlantMonsterHitReactionController(
            HitReactionConfig config,
            CharacterActionState actionState,
            CharacterDamageReceiver ownerDamageReceiver,
            Transform ownerTransform,
            Animator animator,
            ISubscriber<CharacterDamagedMessage> damagedSubscriber)
            : base(
                config,
                actionState,
                null,
                ownerDamageReceiver,
                ownerTransform,
                animator,
                damagedSubscriber)
        {
            this.animator = animator;
        }

        protected override void OnReactionStarted()
        {
        }

        protected override void OnReactionEnded()
        {
        }

        protected override void TriggerHitReactionAnimation(Vector3 hitPoint, Transform attackerTransform)
        {
        }

        public void NotifyHitAnimationStarted() => hasEnteredHitAnimation = false;

        public bool IsHitAnimationFinished
        {
            get
            {
                if (animator == null)
                {
                    return false;
                }

                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash == GotHitState)
                {
                    hasEnteredHitAnimation = true;
                    return false;
                }

                return hasEnteredHitAnimation;
            }
        }
    }
}
