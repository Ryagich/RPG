using System;
using UnityEngine;
using VContainer.Unity;

namespace World.Grass
{
    public sealed class GrassDeformationSource : IStartable, IDisposable
    {
        private readonly CharacterController controller;
        private readonly GrassDeformationRegistry registry;
        private Vector3 previousPosition;
        private bool hasPreviousPosition;
        private bool started;
        private bool disposed;

        public bool IsAnchor { get; }
        public bool IsActive => !disposed && controller != null && controller.gameObject.activeInHierarchy;
        public Vector3 AnchorPosition => controller.transform.position;

        public GrassDeformationSource(CharacterController controller, GrassDeformationRegistry registry, bool isAnchor)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            IsAnchor = isAnchor;
        }

        public void Start()
        {
            if (started || disposed) return;
            started = true;
            registry.Register(this);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ResetHistory();
            if (started) registry.Unregister(this);
        }

        public void ResetHistory() => hasPreviousPosition = false;

        // Called for every registered source, including sources that did not win a GPU slot.
        // History therefore belongs to the entity, never to a reusable slot.
        public bool TrySample(GrassDeformationConfig config, out GrassDeformationSample sample)
        {
            sample = default;
            if (!IsActive || !controller.enabled || !controller.isGrounded || config.SourceStrength <= 0f)
            {
                hasPreviousPosition = false;
                return false;
            }

            var bounds = controller.bounds;
            var position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var previous = hasPreviousPosition ? previousPosition : position;
            if ((position - previous).sqrMagnitude > config.MaxSweepDistance * config.MaxSweepDistance)
                previous = position;

            sample = new GrassDeformationSample(this, previous, position,
                Mathf.Max(bounds.extents.x, bounds.extents.z) * config.SourceRadiusMultiplier, config.SourceStrength);
            previousPosition = position;
            hasPreviousPosition = true;
            return true;
        }
    }

    public readonly struct GrassDeformationSample
    {
        public readonly GrassDeformationSource Source;
        public readonly Vector3 Previous;
        public readonly Vector3 Current;
        public readonly float Radius;
        public readonly float Strength;

        public GrassDeformationSample(GrassDeformationSource source, Vector3 previous,
            Vector3 current, float radius, float strength)
        {
            Source = source;
            Previous = previous;
            Current = current;
            Radius = radius;
            Strength = strength;
        }
    }
}
