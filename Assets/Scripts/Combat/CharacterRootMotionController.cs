using System;
using System.Collections.Generic;
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
        private readonly CharacterController characterController;
        private readonly HashSet<object> activeSources = new();

        private CharacterAnimatorRootMotionRelay relay;

        public CharacterRootMotionController(Animator animator, CharacterController characterController)
        {
            this.animator = animator;
            this.characterController = characterController;
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
            UpdateAnimatorRootMotion();
        }

        public void Dispose()
        {
            activeSources.Clear();
            UpdateAnimatorRootMotion();
            relay?.Unbind(this);
        }

        public void SetRootMotionActive(object source, bool isActive)
        {
            if (source == null)
            {
                return;
            }

            if (isActive)
            {
                activeSources.Add(source);
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
             || characterController == null
             || !characterController.enabled
             || sourceAnimator == null)
            {
                return;
            }

            var localDelta = sourceAnimator.deltaPosition;
            localDelta.y = 0f;

            if (localDelta.sqrMagnitude <= RootMotionThreshold)
            {
                return;
            }

            var worldDelta = sourceAnimator.transform.parent != null
                ? sourceAnimator.transform.parent.TransformVector(localDelta)
                : localDelta;
            worldDelta.y = 0f;

            characterController.Move(worldDelta);
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
