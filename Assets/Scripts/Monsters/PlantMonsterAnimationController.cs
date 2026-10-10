using UnityEngine;
using VContainer.Unity;

namespace Monsters
{
    /// <summary>
    /// The sole runtime API for PlantMonster_1's Animator. State-machine nodes express
    /// monster actions through this class rather than depending on vendor parameter names.
    /// </summary>
    public sealed class PlantMonsterAnimationController : IStartable
    {
        private static readonly int Locomotion = Animator.StringToHash("locomotion");
        private static readonly int IdleBreak = Animator.StringToHash("idleBreak");
        private static readonly int Attack1 = Animator.StringToHash("attack1");
        private static readonly int Attack2 = Animator.StringToHash("attack2");
        private static readonly int Jump = Animator.StringToHash("jump");
        private static readonly int Death = Animator.StringToHash("death");
        private static readonly int GotHit = Animator.StringToHash("gotHit");
        private static readonly int CastStart = Animator.StringToHash("castStart");
        private static readonly int CastEnd = Animator.StringToHash("castEnd");
        private static readonly int Skip = Animator.StringToHash("skip");
        private static readonly int GoPlant = Animator.StringToHash("goPlant");
        private static readonly int GoAlive = Animator.StringToHash("goAlive");
        private static readonly int IdlePlantState = Animator.StringToHash("Base Layer.IdlePlant");
        private static readonly int ComesAliveState = Animator.StringToHash("ComesAlive");
        private static readonly int GoToSleepState = Animator.StringToHash("GoToSleep");
        private static readonly int Attack01State = Animator.StringToHash("Attack01");

        private readonly Animator animator;
        private float locomotion;
        private int expectedActionState;
        private bool expectedActionStateEntered;

        public PlantMonsterAnimationController(Animator animator)
        {
            this.animator = animator;
        }

        public void Start() => SetLocomotion(0f, force: true);

        public void SetLocomotion(float normalizedSpeed, bool force = false)
        {
            var value = Mathf.Clamp01(normalizedSpeed);
            if (animator == null || (!force && Mathf.Approximately(locomotion, value)))
            {
                return;
            }

            locomotion = value;
            animator.SetFloat(Locomotion, locomotion);
        }

        public void PlayIdleBreak() => Trigger(IdleBreak);
        public void PlayIdle()
        {
            if (animator != null)
            {
                animator.Play(IdlePlantState, 0, 0f);
            }
        }

        public bool IsExpectedActionFinished
        {
            get
            {
                if (animator == null || expectedActionState == 0)
                {
                    return false;
                }

                if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == expectedActionState)
                {
                    expectedActionStateEntered = true;
                    return false;
                }

                return expectedActionStateEntered;
            }
        }

        public void PlayPrimaryAttack() => TriggerAction(Attack1, Attack01State);
        public void PlaySecondaryAttack() => Trigger(Attack2);
        public void PlayJump() => Trigger(Jump);
        public void PlayDeath()
        {
            CancelExpectedAction();
            Trigger(Death);
        }
        public void PlayHitReaction() => Trigger(GotHit);
        public void BeginCast() => Trigger(CastStart);
        public void EndCast() => Trigger(CastEnd);
        public void PlaySkip() => Trigger(Skip);
        public void EnterPlantedForm() => TriggerAction(GoPlant, GoToSleepState);
        public void ExitPlantedForm() => TriggerAction(GoAlive, ComesAliveState);

        public void CancelExpectedAction()
        {
            expectedActionState = 0;
            expectedActionStateEntered = false;
        }

        private void TriggerAction(int trigger, int expectedState)
        {
            expectedActionState = expectedState;
            expectedActionStateEntered = false;
            Trigger(trigger);
        }

        private void Trigger(int trigger)
        {
            if (animator != null)
            {
                animator.SetTrigger(trigger);
            }
        }
    }
}
