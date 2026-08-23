using UnityEngine;

namespace Inventory
{
    internal sealed class PlayerWeaponCombatAnimator
    {
        private const string AttackLayerName = "Full Body";
        private static readonly int AttackRequestHash = Animator.StringToHash("Attack");
        private static readonly int HeavyAttackRequestHash = Animator.StringToHash("HeavyAttack");
        private static readonly int DodgeRequestHash = Animator.StringToHash("Dodge");
        private static readonly int RollRequestHash = Animator.StringToHash("Roll");
        private static readonly int EmptyIdleStateHash = Animator.StringToHash("Empty Idle");
        private static readonly int DodgeStateHash = Animator.StringToHash("Dodge Tree");
        private static readonly int RollStateHash = Animator.StringToHash("Dodge RollTree");
        private static readonly int HeavyAttackHitRootMotionStateHash = Animator.StringToHash("A_Attack_HeavyCombo01A_Hit_RootMotion_Sword");
        private static readonly int HeavyAttackHitStateHash = Animator.StringToHash("A_Attack_HeavyCombo01B_Hit_Sword");

        private static readonly int[] HitStateHashes =
        {
            Animator.StringToHash("A_Hit_B_Stagger_RootMotion_Sword"),
            Animator.StringToHash("A_Hit_F_Stagger_RootMotion_Sword"),
            Animator.StringToHash("A_Hit_R_Stagger_RootMotion_Sword"),
            Animator.StringToHash("A_Hit_L_Stagger_RootMotion_Sword"),
            Animator.StringToHash("A_Attack_LightCombo01B_Hit_Sword"),
            Animator.StringToHash("A_Attack_LightCombo01C_Hit_Sword"),
            HeavyAttackHitRootMotionStateHash,
            HeavyAttackHitStateHash
        };

        private readonly Animator animator;

        public PlayerWeaponCombatAnimator(Animator animator)
        {
            this.animator = animator;
            LayerIndex = animator != null ? animator.GetLayerIndex(AttackLayerName) : -1;
        }

        public int LayerIndex { get; }

        public void SetRequests(bool lightAttackRequested, bool heavyAttackRequested, bool dodgeRequested, bool rollRequested)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(AttackRequestHash, lightAttackRequested);
            animator.SetBool(HeavyAttackRequestHash, heavyAttackRequested);
            animator.SetBool(DodgeRequestHash, dodgeRequested);
            animator.SetBool(RollRequestHash, rollRequested);
        }

        public bool IsAnyRequestActive()
        {
            return animator != null
                   && (animator.GetBool(AttackRequestHash)
                       || animator.GetBool(HeavyAttackRequestHash)
                       || animator.GetBool(DodgeRequestHash)
                       || animator.GetBool(RollRequestHash));
        }

        public bool IsFullBodyActionActive() => IsCurrentOrNextState(IsActionState);
        public bool IsHitActive() => IsCurrentOrNextState(IsHitState);
        public bool IsHeavyAttackHitActive() => IsCurrentOrNextState(IsHeavyAttackHitState);

        public bool IsDodgeActive()
        {
            return IsRequestSet(DodgeRequestHash) || IsCurrentOrNextState(state => state.shortNameHash == DodgeStateHash);
        }

        public bool IsRollActive()
        {
            return IsRequestSet(RollRequestHash) || IsCurrentOrNextState(state => state.shortNameHash == RollStateHash);
        }

        private bool IsRequestSet(int parameterHash) => animator != null && animator.GetBool(parameterHash);

        private bool IsCurrentOrNextState(System.Func<AnimatorStateInfo, bool> predicate)
        {
            if (animator == null || LayerIndex < 0)
            {
                return false;
            }

            if (predicate(animator.GetCurrentAnimatorStateInfo(LayerIndex)))
            {
                return true;
            }

            return animator.IsInTransition(LayerIndex)
                   && predicate(animator.GetNextAnimatorStateInfo(LayerIndex));
        }

        private static bool IsActionState(AnimatorStateInfo state) => state.shortNameHash != 0 && state.shortNameHash != EmptyIdleStateHash;

        private static bool IsHitState(AnimatorStateInfo state)
        {
            var stateHash = state.shortNameHash;
            foreach (var hitStateHash in HitStateHashes)
            {
                if (stateHash == hitStateHash)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHeavyAttackHitState(AnimatorStateInfo state)
        {
            return state.shortNameHash == HeavyAttackHitRootMotionStateHash
                   || state.shortNameHash == HeavyAttackHitStateHash;
        }
    }
}
