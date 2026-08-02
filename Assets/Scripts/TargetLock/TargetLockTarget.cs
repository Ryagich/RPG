using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace TargetLock
{
    public interface ITargetLockTargetRegistry
    {
        IReadOnlyCollection<TargetLockTarget> Targets { get; }
        void Register(TargetLockTarget target);
        void Unregister(TargetLockTarget target);
    }

    public sealed class TargetLockTargetRegistry : ITargetLockTargetRegistry
    {
        private readonly HashSet<TargetLockTarget> targets = new();

        public IReadOnlyCollection<TargetLockTarget> Targets => targets;

        public void Register(TargetLockTarget target)
        {
            if (target != null)
            {
                targets.Add(target);
            }
        }

        public void Unregister(TargetLockTarget target)
        {
            if (target != null)
            {
                targets.Remove(target);
            }
        }
    }

    public sealed class TargetLockTarget : MonoBehaviour
    {
        [SerializeField] private Transform aimPoint;
        [SerializeField] private Vector3 aimOffset = new(0f, 1.35f, 0f);

        private ITargetLockTargetRegistry registry;

        public Transform AimTransform => aimPoint != null ? aimPoint : transform;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + aimOffset;
        public bool IsTargetable => isActiveAndEnabled && gameObject.activeInHierarchy;

        [Inject]
        public void Construct(ITargetLockTargetRegistry targetRegistry)
        {
            if (registry == targetRegistry)
            {
                return;
            }

            registry?.Unregister(this);
            registry = targetRegistry;
            if (isActiveAndEnabled)
            {
                registry.Register(this);
            }
        }

        private void OnEnable()
        {
            registry?.Register(this);
        }

        private void OnDisable()
        {
            registry?.Unregister(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(AimPosition, 0.35f);
        }
    }
}
