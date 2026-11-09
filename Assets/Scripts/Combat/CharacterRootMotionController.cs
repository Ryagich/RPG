using System;
using System.Collections.Generic;
using CharacterAnimation;
using Movement;
using UnityEngine;
using VContainer.Unity;

namespace Combat
{
    [DisallowMultipleComponent]
    public sealed class CharacterAnimatorRootMotionRelay : MonoBehaviour
    {
        private Animator animator;
        private CharacterRootMotionController controller;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void Bind(CharacterRootMotionController rootMotionController)
        {
            controller = rootMotionController;

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        public void Unbind(CharacterRootMotionController rootMotionController)
        {
            if (controller == rootMotionController)
            {
                controller = null;
            }
        }

        private void OnAnimatorMove()
        {
            if (animator == null || controller == null || !animator.applyRootMotion)
            {
                return;
            }

            controller.ApplyAnimatorMove(animator);
        }
    }

    public sealed class CharacterRootMotionController : IStartable, IDisposable
    {
        private const float RootMotionThreshold = 0.000001f;

        private readonly Animator animator;
        private readonly CharacterMotor motor;
        private readonly Dictionary<object, float> activeSources = new();

        private CharacterAnimatorRootMotionRelay relay;
        private CharacterAnimationEventReceiver animationEventReceiver;

        public CharacterRootMotionController(Animator animator, CharacterMotor motor)
        {
            this.animator = animator;
            this.motor = motor;
        }

        public void Start()
        {
            if (animator == null)
            {
                return;
            }

            relay = animator.GetComponent<CharacterAnimatorRootMotionRelay>()
                 ?? animator.gameObject.AddComponent<CharacterAnimatorRootMotionRelay>();
            relay.Bind(this);
            animationEventReceiver = animator.GetComponent<CharacterAnimationEventReceiver>();
            animationEventReceiver?.BindRootMotion(this);
            UpdateAnimatorRootMotion();
        }

        public void Dispose()
        {
            activeSources.Clear();
            UpdateAnimatorRootMotion();
            relay?.Unbind(this);
            animationEventReceiver?.UnbindRootMotion(this);
        }

        /// <summary>
        /// Enables root motion for a source. Each source can specify how much of its Animator
        /// displacement should be applied; overlapping sources use the largest multiplier.
        /// </summary>
        public void SetRootMotionActive(object source, bool isActive, float positionMultiplier = 1f)
        {
            if (source == null)
            {
                return;
            }

            if (isActive)
            {
                activeSources[source] = Mathf.Max(0f, positionMultiplier);
            }
            else
            {
                activeSources.Remove(source);
            }

            UpdateAnimatorRootMotion();
        }

        public void ApplyAnimatorMove(Animator sourceAnimator)
        {
            if (activeSources.Count == 0
             || sourceAnimator == null)
            {
                return;
            }

            // Animator provides root motion as a world-space delta. CharacterMotor resolves it
            // together with the other motion contributors in its one Move call per frame.
            var worldDelta = sourceAnimator.deltaPosition;
            worldDelta.y = 0f;

            if (worldDelta.sqrMagnitude <= RootMotionThreshold)
            {
                return;
            }

            motor?.SubmitRootMotion(worldDelta * GetActivePositionMultiplier());
        }

        private float GetActivePositionMultiplier()
        {
            var multiplier = 0f;

            foreach (var sourceMultiplier in activeSources.Values)
            {
                multiplier = Mathf.Max(multiplier, sourceMultiplier);
            }

            return multiplier;
        }

        private void UpdateAnimatorRootMotion()
        {
            if (animator != null)
            {
                animator.applyRootMotion = activeSources.Count > 0;
            }
        }
    }
}
