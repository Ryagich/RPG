using Interactable;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Inventory.Looting
{
    [DisallowMultipleComponent]
    public sealed class CorpseLootAvailability : MonoBehaviour, IInteractableAvailability
    {
        private CorpseLootController corpseLootController;

        [Inject]
        public void Construct(CorpseLootController corpseLootController)
        {
            this.corpseLootController = corpseLootController;
        }

        public bool IsInteractableAvailable(LifetimeScope interactorScope)
        {
            return corpseLootController?.IsLootable == true
                && corpseLootController.LootInventory != null;
        }
    }
}
