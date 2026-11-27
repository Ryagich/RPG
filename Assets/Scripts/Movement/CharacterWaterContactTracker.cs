using System.Collections.Generic;
using UnityEngine;

namespace Movement
{
    /// <summary>
    /// Vertical bounds of one active water trigger. Water state selection needs both values:
    /// the surface alone cannot distinguish a deep volume from a shallow one on the first
    /// frame of contact.
    /// </summary>
    public readonly struct CharacterWaterVolume
    {
        public CharacterWaterVolume(float surfaceHeight, float bottomHeight)
        {
            SurfaceHeight = surfaceHeight;
            BottomHeight = bottomHeight;
        }

        public float SurfaceHeight { get; }
        public float BottomHeight { get; }
        public float Depth => Mathf.Max(0f, SurfaceHeight - BottomHeight);
    }

    /// <summary>
    /// Tracks water trigger contacts for one CharacterController. Several water colliders may
    /// overlap at seams; the highest active trigger boundary is the current water surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterWaterContactTracker : MonoBehaviour
    {
        private readonly HashSet<Collider> activeWaterTriggers = new();

        private int waterLayer;

        private void Awake()
        {
            waterLayer = LayerMask.NameToLayer("Water");
        }

        private void OnDisable()
        {
            activeWaterTriggers.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsWaterTrigger(other))
            {
                activeWaterTriggers.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            activeWaterTriggers.Remove(other);
        }

        public bool TryGetWaterVolume(out CharacterWaterVolume waterVolume)
        {
            RemoveInvalidContacts();
            if (activeWaterTriggers.Count == 0)
            {
                waterVolume = default;
                return false;
            }

            var surfaceHeight = float.MinValue;
            var bottomHeight = float.MaxValue;
            foreach (var waterTrigger in activeWaterTriggers)
            {
                var bounds = waterTrigger.bounds;
                if (bounds.max.y > surfaceHeight)
                {
                    surfaceHeight = bounds.max.y;
                    bottomHeight = bounds.min.y;
                }
                else if (Mathf.Approximately(bounds.max.y, surfaceHeight))
                {
                    bottomHeight = Mathf.Min(bottomHeight, bounds.min.y);
                }
            }

            waterVolume = new CharacterWaterVolume(surfaceHeight, bottomHeight);
            return true;
        }

        private bool IsWaterTrigger(Collider collider)
        {
            return collider != null
                   && collider.isTrigger
                   && waterLayer >= 0
                   && collider.gameObject.layer == waterLayer;
        }

        private void RemoveInvalidContacts()
        {
            activeWaterTriggers.RemoveWhere(waterTrigger => !IsWaterTrigger(waterTrigger));
        }
    }

    /// <summary>
    /// Answers whether there is standable physical ground directly below the water surface.
    /// It deliberately uses the character root rather than CharacterController contact, so
    /// water-state selection remains valid when the controller changes for swimming.
    /// </summary>
    public sealed class CharacterWaterBottomProbe
    {
        private const float SurfaceProbeOffset = 0.05f;
        private const int HitCapacity = 8;

        private readonly CharacterWaterContactTracker waterContactTracker;
        private readonly CharacterWaterConfig waterConfig;
        private readonly RaycastHit[] hits = new RaycastHit[HitCapacity];

        public CharacterWaterBottomProbe(
            CharacterWaterContactTracker waterContactTracker,
            CharacterWaterConfig waterConfig)
        {
            this.waterContactTracker = waterContactTracker;
            this.waterConfig = waterConfig;
        }

        public bool HasStandableBottom(float surfaceHeight)
        {
            if (waterContactTracker == null || waterConfig == null)
            {
                return false;
            }

            var characterRoot = waterContactTracker.transform;
            var origin = characterRoot.position;
            origin.y = surfaceHeight + SurfaceProbeOffset;
            // A bottom anywhere in the complete wading state range must prevent a deep-water
            // trigger from switching the character to swimming. Wading starts at its own entry
            // threshold and ends at Swim entry, while Swim exit may be deeper in a custom config.
            var maximumStandableBottomDepth = Mathf.Max(
                waterConfig.WadingEnterSubmersionHeight,
                Mathf.Max(
                    waterConfig.SwimEnterSubmersionHeight,
                    waterConfig.SwimExitSubmersionHeight));
            var maxDistance = SurfaceProbeOffset + maximumStandableBottomDepth;
            var hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                hits,
                maxDistance,
                waterConfig.WadingBottomProbeMask,
                QueryTriggerInteraction.Ignore);

            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = hits[index].collider;
                if (hitCollider != null && !BelongsToCharacter(hitCollider.transform, characterRoot))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BelongsToCharacter(Transform candidate, Transform characterRoot)
        {
            return candidate == characterRoot
                   || candidate.IsChildOf(characterRoot)
                   || characterRoot.IsChildOf(candidate);
        }
    }
}
