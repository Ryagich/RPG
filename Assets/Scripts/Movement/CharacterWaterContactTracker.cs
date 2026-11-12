using System.Collections.Generic;
using UnityEngine;

namespace Movement
{
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

        public bool TryGetSurfaceHeight(out float surfaceHeight)
        {
            RemoveInvalidContacts();
            if (activeWaterTriggers.Count == 0)
            {
                surfaceHeight = 0f;
                return false;
            }

            surfaceHeight = float.MinValue;
            foreach (var waterTrigger in activeWaterTriggers)
            {
                surfaceHeight = Mathf.Max(surfaceHeight, waterTrigger.bounds.max.y);
            }

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
}
