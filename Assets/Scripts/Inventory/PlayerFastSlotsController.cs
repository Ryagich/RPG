using System.Linq;
using Inventory.Inventories;
using Inventory.Item;
using MessagePipe;
using Messages;
using VContainer.Unity;

using Combat;

namespace Inventory
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class PlayerFastSlotsController : IStartable
    {
        private readonly PlayerInventory playerInventory;
        private readonly InventoryHandController inventoryHandController;
        private readonly CharacterActionState actionState;
        private bool isPlayerDead;

        public PlayerFastSlotsController(
            PlayerInventory playerInventory,
            InventoryHandController inventoryHandController,
            CharacterActionState actionState,
            ISubscriber<FastSlotInputMessage> fastSlotInputSubscriber,
            ISubscriber<PlayerDiedMessage> playerDiedSubscriber)
        {
            this.playerInventory = playerInventory;
            this.inventoryHandController = inventoryHandController;
            this.actionState = actionState;

            fastSlotInputSubscriber.Subscribe(OnFastSlotInput);
            playerDiedSubscriber.Subscribe(_ => isPlayerDead = true);
        }

        public void Start() { }

        private void OnFastSlotInput(FastSlotInputMessage message)
        {
            if (isPlayerDead || actionState.IsActionBlocked)
            {
                return;
            }

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
