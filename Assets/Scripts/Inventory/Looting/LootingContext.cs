using Character;
using Inventory.Inventories;
using Inventory.Slot;
using Factions;
using Money;

namespace Inventory.Looting
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class LootingContext
    {
        public IInventory CurrentTargetInventory { get; private set; }
        public CharacterInfo CurrentTargetCharacterInfo { get; private set; }
        public FactionConfig CurrentTargetFaction { get; private set; }
        public MoneyStorage CurrentTargetMoneyStorage { get; private set; }

        public void SetTarget(
            IInventory inventory,
            CharacterInfo characterInfo = null,
            MoneyStorage moneyStorage = null,
            FactionConfig faction = null)
        {
            CurrentTargetInventory = inventory;
            CurrentTargetCharacterInfo = characterInfo;
            CurrentTargetMoneyStorage = moneyStorage;
            CurrentTargetFaction = faction;
        }

        public void Clear()
        {
            CurrentTargetInventory = null;
            CurrentTargetCharacterInfo = null;
            CurrentTargetMoneyStorage = null;
            CurrentTargetFaction = null;
        }
    }

    /// <summary>
    /// Applies inventory-to-inventory loot transfers. It owns capacity checks, reversible slot
    /// transfers, and player-backpack overflow; UI only selects the stack and requests a move.
    /// </summary>
    public sealed class LootTransferService
    {
        private readonly PlayerInventory playerInventory;
        private readonly System.Action<Inventory.Item.ItemStack> dropOverflow;

        public LootTransferService(PlayerInventory playerInventory, System.Action<Inventory.Item.ItemStack> dropOverflow)
        {
            this.playerInventory = playerInventory ?? throw new System.ArgumentNullException(nameof(playerInventory));
            this.dropOverflow = dropOverflow;
        }

        public IInventory GetCounterpart(IInventory targetInventory, IInventory sourceInventory)
        {
            if (sourceInventory == playerInventory)
            {
                return targetInventory;
            }

            return sourceInventory == targetInventory ? playerInventory : null;
        }

        public Inventory.Item.ItemStack TryStoreAcrossInventories(IInventory targetInventory, Inventory.Item.ItemStack itemStack)
        {
            if (itemStack?.ItemConfig == null)
            {
                return null;
            }

            var remainder = playerInventory.TryAdd(itemStack);
            return remainder != null && targetInventory != null
                ? targetInventory.TryAdd(remainder)
                : remainder;
        }

        public bool CanMove(IInventory targetInventory, IInventory sourceInventory, Inventory.Item.ItemStack itemStack)
        {
            return CanInventoryAcceptAny(GetCounterpart(targetInventory, sourceInventory), itemStack);
        }

        public bool TryMovePlayerSlot(IInventory targetInventory, Inventory.Slot.SlotModel slotModel, int count)
        {
            var destinationInventory = GetCounterpart(targetInventory, playerInventory);
            if (destinationInventory == null
                || slotModel?.ItemStack?.ItemConfig == null
                || !playerInventory.TryTakeFromSlot(slotModel, count, out var itemStack))
            {
                return false;
            }

            var remainder = destinationInventory.TryAdd(itemStack);
            if (remainder != null)
            {
                RestorePlayerSlot(slotModel, remainder);
                return false;
            }

            if (slotModel.ItemType == Inventory.Item.ItemType.Backpack)
            {
                DropPlayerBackpackOverflow();
            }

            return true;
        }

        public bool TryMoveInventoryItem(
            IInventory targetInventory,
            IInventory sourceInventory,
            Inventory.Item.ItemInInventory itemInInventory,
            int count)
        {
            return TryMoveInventoryItem(targetInventory, sourceInventory, itemInInventory, count, null);
        }

        public bool TryMoveInventoryItem(
            IInventory targetInventory,
            IInventory sourceInventory,
            Inventory.Item.ItemInInventory itemInInventory,
            int count,
            System.Func<SlotModel, UnityEngine.Vector2?> slotSizeProvider)
        {
            if (sourceInventory == null || itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var destinationInventory = GetCounterpart(targetInventory, sourceInventory);
            if (destinationInventory == null)
            {
                return false;
            }

            var itemConfig = itemInInventory.ItemConfig;
            var isRotated = itemInInventory.ItemStack.IsRotated;
            var runtimeTag = itemInInventory.ItemStack.RuntimeTag;
            var originalPosition = itemInInventory.Position;
            var originalCount = itemInInventory.Count;
            var moveCount = UnityEngine.Mathf.Clamp(count, 1, originalCount);
            var movingStack = new Inventory.Item.ItemStack(itemConfig, moveCount, isRotated, runtimeTag);
            var remainder = ReferenceEquals(destinationInventory, playerInventory)
                ? playerInventory.TryAdd(movingStack, slotSizeProvider)
                : destinationInventory.TryAdd(movingStack);
            var movedCount = moveCount - (remainder?.Count ?? 0);
            if (movedCount <= 0)
            {
                return false;
            }

            sourceInventory.Remove(itemInInventory);
            var remainingSourceCount = originalCount - movedCount;
            if (remainingSourceCount > 0)
            {
                sourceInventory.Add(new Inventory.Item.ItemStack(itemConfig, remainingSourceCount, isRotated, runtimeTag), originalPosition);
            }

            return true;
        }

        private void RestorePlayerSlot(Inventory.Slot.SlotModel slotModel, Inventory.Item.ItemStack itemStack)
        {
            if (slotModel == null || itemStack?.ItemConfig == null)
            {
                return;
            }

            playerInventory.TryPlaceInSlot(slotModel, itemStack, out var remainderStack, out var replacedStack);
            DropIfOverflow(replacedStack);
            DropIfOverflow(remainderStack);
        }

        private void DropIfOverflow(Inventory.Item.ItemStack itemStack)
        {
            if (itemStack == null)
            {
                return;
            }

            var remainder = playerInventory.TryAdd(itemStack);
            if (remainder != null)
            {
                dropOverflow?.Invoke(remainder);
            }
        }

        private void DropPlayerBackpackOverflow()
        {
            foreach (var itemStack in playerInventory.RebuildInventoryFromCurrentBackpack())
            {
                dropOverflow?.Invoke(itemStack);
            }
        }

        private static bool CanInventoryAcceptAny(IInventory inventory, Inventory.Item.ItemStack itemStack)
        {
            if (inventory == null || itemStack?.ItemConfig == null)
            {
                return false;
            }

            return inventory switch
            {
                PlayerInventory playerDestination => CanPlayerInventoryAcceptAny(playerDestination, itemStack),
                ITiledInventory tiledDestination => CanTiledInventoryAcceptAny(tiledDestination, itemStack),
                _ => false
            };
        }

        private static bool CanPlayerInventoryAcceptAny(PlayerInventory inventory, Inventory.Item.ItemStack itemStack)
        {
            if (itemStack.ItemConfig.ItemType == Inventory.Item.ItemType.Helm
                && itemStack.ItemConfig.BlocksFaceSlot
                && inventory.FaceSlot.ItemConfig != null
                && !inventory.CanMoveSlotItemToGrid(inventory.FaceSlot))
            {
                return false;
            }

            foreach (var slot in new[]
                     {
                         inventory.HelmSlot, inventory.FaceSlot, inventory.BodySlot, inventory.HandsSlot,
                         inventory.ArmsSlot, inventory.LegsSlot, inventory.HipsSlot, inventory.BackpackSlot,
                         inventory.LeftWeaponSlot, inventory.RightWeaponSlot
                     })
            {
                if (inventory.IsSlotBlocked(slot) || slot.ItemType != itemStack.ItemConfig.ItemType)
                {
                    continue;
                }

                if (slot.ItemStack == null || (slot.ItemStack.CanStackWith(itemStack) && slot.ItemStack.Count < slot.GetMaxStack(itemStack.ItemConfig)))
                {
                    return true;
                }
            }

            return CanTiledInventoryAcceptAny(inventory, itemStack);
        }

        private static bool CanTiledInventoryAcceptAny(ITiledInventory inventory, Inventory.Item.ItemStack itemStack)
        {
            if (inventory == null || itemStack?.ItemConfig == null)
            {
                return false;
            }

            foreach (var item in inventory.Items)
            {
                if (item?.ItemStack?.CanStackWith(itemStack) == true && !item.ItemStack.IsFull)
                {
                    return true;
                }
            }

            return InventoryGridPlacement.CanFit(inventory.Tiles, itemStack);
        }
    }

    /// <summary>
    /// Coordinates loot-page double-click intents. A failed transfer is resolved through the
    /// same inventory actions used by the inventory page, not by the UI itself.
    /// </summary>
    public sealed class LootingDoubleClickActionService
    {
        private readonly LootTransferService lootTransferService;
        private readonly System.Func<IInventory, Inventory.Item.ItemInInventory, bool> tryUseInventoryItem;
        private readonly System.Func<SlotModel, bool> tryMoveSlotToGrid;

        public LootingDoubleClickActionService(
            LootTransferService lootTransferService,
            System.Func<IInventory, Inventory.Item.ItemInInventory, bool> tryUseInventoryItem,
            System.Func<SlotModel, bool> tryMoveSlotToGrid)
        {
            this.lootTransferService = lootTransferService ?? throw new System.ArgumentNullException(nameof(lootTransferService));
            this.tryUseInventoryItem = tryUseInventoryItem ?? throw new System.ArgumentNullException(nameof(tryUseInventoryItem));
            this.tryMoveSlotToGrid = tryMoveSlotToGrid ?? throw new System.ArgumentNullException(nameof(tryMoveSlotToGrid));
        }

        public bool TryMoveOrUseInventoryItem(
            IInventory targetInventory,
            IInventory sourceInventory,
            Inventory.Item.ItemInInventory itemInInventory,
            int count,
            System.Func<SlotModel, UnityEngine.Vector2?> slotSizeProvider)
        {
            if (lootTransferService.TryMoveInventoryItem(
                    targetInventory,
                    sourceInventory,
                    itemInInventory,
                    count,
                    slotSizeProvider))
            {
                return true;
            }

            return tryUseInventoryItem(sourceInventory, itemInInventory);
        }

        public bool TryMoveOrStorePlayerSlot(
            IInventory targetInventory,
            SlotModel slotModel,
            int count)
        {
            if (slotModel?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            return lootTransferService.TryMovePlayerSlot(targetInventory, slotModel, count)
                   || tryMoveSlotToGrid(slotModel);
        }
    }
}
