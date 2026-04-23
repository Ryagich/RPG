using System.Linq;
using Inventory.Inventories;
using Inventory.Item;
using MessagePipe;
using Messages;
using VContainer.Unity;

namespace Inventory
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class PlayerFastSlotsController : IStartable
    {
        private readonly PlayerInventory playerInventory;
        private readonly InventoryHandController inventoryHandController;

        public PlayerFastSlotsController(
            PlayerInventory playerInventory,
            InventoryHandController inventoryHandController,
            ISubscriber<FastSlotInputMessage> fastSlotInputSubscriber)
        {
            this.playerInventory = playerInventory;
            this.inventoryHandController = inventoryHandController;

            fastSlotInputSubscriber.Subscribe(OnFastSlotInput);
        }

        public void Start() { }

        private void OnFastSlotInput(FastSlotInputMessage message)
        {
            if (!playerInventory.TryGetFastSlot(message.SlotIndex, out var fastSlot)
             || fastSlot?.ItemConfig == null
             || fastSlot.ItemConfig.ItemType != ItemType.Usable
             || playerInventory.HandSlot.Value?.ItemStack != null
             || !playerInventory.TryFindFirstItem(fastSlot.ItemConfig, out var itemInInventory)
             || itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            var sourceStack = itemInInventory.ItemStack;
            var originalPosition = itemInInventory.Position;
            var originalCount = sourceStack.Count;
            var usedStack = new ItemStack(sourceStack.ItemConfig, 1, sourceStack.IsRotated);

            playerInventory.Remove(itemInInventory);
            if (!inventoryHandController.TryUseFromInventory(usedStack))
            {
                playerInventory.Add(new ItemStack(sourceStack.ItemConfig, originalCount, sourceStack.IsRotated), originalPosition);
                return;
            }

            if (originalCount > 1)
            {
                playerInventory.Add(new ItemStack(sourceStack.ItemConfig, originalCount - 1, sourceStack.IsRotated), originalPosition);
            }
        }
    }
}
