using Container;
using UnityEngine;
using VContainer;

namespace Inventory
{
    /// <summary>
    /// Trigger-zone boundary that registers its presence in the player's scoped weapon state.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class WeaponDrawingBlockedZone : MonoBehaviour
    {
        private PlayerWeaponDrawingBlockState registeredState;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerControllerCollider(other))
            {
                return;
            }

            var playerScope = other.GetComponent<PlayerLifetimeScope>();
            registeredState = playerScope.Container.Resolve<PlayerWeaponDrawingBlockState>();
            registeredState.RegisterZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayerControllerCollider(other))
            {
                return;
            }

            UnregisterFromPlayer();
        }

        private void OnDisable()
        {
            UnregisterFromPlayer();
        }

        private void OnDestroy()
        {
            UnregisterFromPlayer();
        }

        private void UnregisterFromPlayer()
        {
            if (registeredState == null)
            {
                return;
            }

            registeredState.UnregisterZone(this);
            registeredState = null;
        }

        private static bool IsPlayerControllerCollider(Collider other)
        {
            return other.GetComponent<CharacterController>() != null
                   && other.GetComponent<PlayerLifetimeScope>() != null;
        }
    }
}
