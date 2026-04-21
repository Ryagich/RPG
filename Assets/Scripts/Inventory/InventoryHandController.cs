using System.Collections.Generic;
using GameModes;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using MessagePipe;
using Messages;
using UI.Inventory;
using UI.Pages;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Inventory
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InventoryHandController : IStartable
    {
        private const float ThrowForwardOffset = 1.25f;
        private const float ThrowUpOffset = 1f;
        private const float ThrowForce = 2.5f;

        private readonly PlayerInventory playerInventory;
        private readonly Transform playerTransform;
        private readonly GameModesController gameModesController;
        private bool backpackResizePendingAfterHandAction;
        private ItemStack backpackTakenFromSlot;
        private IInventory handSourceInventory;
        private SlotModel handSourceSlot;
        private Matrix4x4 handSourcePosition;
        private MouseButtonType? activeHandButton;

        public InventoryHandController(
            PlayerInventory playerInventory,
            Transform playerTransform,
            GameModesController gameModesController,
            ISubscriber<MouseDown> mouseDownSubscriber,
            ISubscriber<MouseUp> mouseUpSubscriber,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.playerInventory = playerInventory;
            this.playerTransform = playerTransform;
            this.gameModesController = gameModesController;

            mouseDownSubscriber.Subscribe(OnMouseDown);
            mouseUpSubscriber.Subscribe(OnMouseUp);
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
        }

        public void Start() { }

        private void OnMouseDown(MouseDown message)
        {
            CaptureGrabOffset();

            if (!IsInventoryInteractionMode(gameModesController.GameMode) || HasItemInHand())
            {
                return;
            }

            handSourceInventory = null;
            handSourceSlot = null;
            handSourcePosition = Matrix4x4.identity;
            activeHandButton = null;
            playerInventory.HandSourceInventory.Value = null;

            var interactionPage = GetCurrentInteractionPage();
            var pointer = Pointer.current;
            if (interactionPage != null
             && pointer != null
             && interactionPage.TryHandleMouseDown(message.Button, pointer.position.ReadValue()))
            {
                return;
            }

            if (interactionPage != null
             && pointer != null
             && interactionPage.TryGetHoveredSlot(pointer.position.ReadValue(), out var slotModel)
             && TryTakeFromHoveredSlot(slotModel, message.Button, out var slotItemStack))
            {
                if (slotModel.ItemType == ItemType.Backpack)
                {
                    backpackResizePendingAfterHandAction = true;
                    backpackTakenFromSlot = slotItemStack;
                }

                handSourceInventory = playerInventory;
                handSourceSlot = slotModel;
                playerInventory.HandSourceInventory.Value = handSourceInventory;
                playerInventory.HandSlot.Value = new SlotModel(slotItemStack.ItemConfig.ItemType, SlotStackLimitType.SingleItem, slotItemStack);
                activeHandButton = message.Button;
                TradePage.Current?.SetDragSource(handSourceInventory, handSourceSlot);
                return;
            }

            if (!TryTakeFromHoveredTile(message.Button, out var hoveredInventory, out var itemInInventory, out var itemStack))
            {
                return;
            }

            handSourceInventory = hoveredInventory;
            handSourcePosition = itemInInventory.Position;
            playerInventory.HandSourceInventory.Value = handSourceInventory;
            playerInventory.HandSlot.Value = new SlotModel(itemInInventory.ItemConfig.ItemType, SlotStackLimitType.SingleItem, itemStack);
            activeHandButton = message.Button;
            TradePage.Current?.SetDragSource(handSourceInventory, handSourceSlot);
        }

        private void OnMouseUp(MouseUp message)
        {
            if (!IsInventoryInteractionMode(gameModesController.GameMode)
             || !HasItemInHand()
             || activeHandButton != message.Button)
            {
                return;
            }

            var itemStack = playerInventory.HandSlot.Value.ItemStack;
            if (gameModesController.GameMode == GameMode.Trade)
            {
                HandleTradeMouseUp(itemStack);
                return;
            }

            if (TryAddToHoveredSlot(itemStack))
            {
                return;
            }

            if (TryAddToHoveredTile(itemStack))
            {
                return;
            }

            if (IsPointerOverSlot() && TryStoreOrDrop(itemStack, playerInventory.TryAdd(itemStack)))
            {
                return;
            }

            if (IsPointerOverInventory() && TryStoreOrDrop(itemStack, playerInventory.TryAddToGrid(itemStack)))
            {
                return;
            }

            var interactionPage = GetCurrentInteractionPage();
            var pointer = Pointer.current;
            if (interactionPage != null
             && pointer != null
             && interactionPage.IsInPlayerSections(pointer.position.ReadValue())
             && TryStoreOrDrop(itemStack, playerInventory.TryAdd(itemStack)))
            {
                return;
            }

            if (LootingPage.Current != null
             && pointer != null
             && LootingPage.Current.IsInTargetSection(pointer.position.ReadValue()))
            {
                var targetInventory = LootingPage.Current.GetTargetInventory();
                if (targetInventory != null && TryStoreOrDrop(itemStack, targetInventory.TryAdd(itemStack)))
                {
                    return;
                }
            }

            ThrowItem(itemStack);
        }

        private bool TryAddToHoveredSlot(ItemStack itemStack)
        {
            var interactionPage = GetCurrentInteractionPage();
            var pointer = Pointer.current;
            if (interactionPage == null
             || pointer == null
             || !interactionPage.TryGetHoveredSlot(pointer.position.ReadValue(), out var slotModel)
             || !playerInventory.TryPlaceInSlot(slotModel.ItemType, itemStack, out var remainderStack, out var replacedStack))
            {
                return false;
            }

            if (slotModel.ItemType == ItemType.Backpack)
            {
                RebuildInventoryAndThrowOverflowItems();
            }

            if (replacedStack != null)
            {
                var replacedRemainder = playerInventory.TryAdd(replacedStack);
                if (replacedRemainder != null)
                {
                    SpawnItemInWorld(replacedRemainder);
                }
            }

            if (remainderStack != null)
            {
                var remainderAfterInventory = playerInventory.TryAdd(remainderStack);
                if (remainderAfterInventory != null)
                {
                    SpawnItemInWorld(remainderAfterInventory);
                }
            }

            ClearHand();
            return true;
        }

        private static bool IsPointerOverInventory()
        {
            return IsPointerOver<InventoryView>();
        }

        private static bool IsPointerOverSlot()
        {
            return IsPointerOver<SlotView>();
        }

        private static bool IsPointerOver<T>() where T : Component
        {
            if (EventSystem.current == null || Pointer.current == null)
            {
                return false;
            }

            var pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = Pointer.current.position.ReadValue()
            };
            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            foreach (var raycastResult in raycastResults)
            {
                if (raycastResult.gameObject.GetComponentInParent<T>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            if (IsInventoryInteractionMode(msg.GameMode) || !HasItemInHand())
            {
                return;
            }

            var itemStack = playerInventory.HandSlot.Value.ItemStack;
            if (TryStoreOrDrop(itemStack, playerInventory.TryAdd(itemStack)))
            {
                return;
            }

            ThrowItem(itemStack);
        }

        private bool TryAddToHoveredTile(ItemStack itemStack)
        {
            if (!InventoryTilePointerHandler.TryGetHovered(out var hoveredInventory, out _))
            {
                return false;
            }

            var interactionPage = GetCurrentInteractionPage();
            var pointer = Pointer.current;
            if (interactionPage == null || pointer == null)
            {
                return false;
            }

            if (!interactionPage.TryGetPlacementTile(pointer.position.ReadValue(), hoveredInventory, out var placementTile))
            {
                return false;
            }

            var remainder = hoveredInventory.TryAdd(itemStack, placementTile);
            return TryStoreOrDrop(itemStack, remainder, hoveredInventory);
        }

        private bool HasItemInHand() => playerInventory.HandSlot.Value?.ItemStack != null;

        private void ClearHand()
        {
            playerInventory.HandSlot.Value = new SlotModel(ItemType.None, SlotStackLimitType.SingleItem, null);
            handSourceInventory = null;
            handSourceSlot = null;
            handSourcePosition = Matrix4x4.identity;
            activeHandButton = null;
            playerInventory.HandSourceInventory.Value = null;
            TradePage.Current?.ClearDragSource();
            GetCurrentInteractionPage()?.ResetGrabOffset();
            ProcessDelayedBackpackResize();
        }

        private bool TryTakeFromHoveredSlot(SlotModel slotModel, MouseButtonType button, out ItemStack itemStack)
        {
            itemStack = null;
            if (slotModel == null)
            {
                return false;
            }

            if (button == MouseButtonType.Left)
            {
                return playerInventory.TryTakeFromSlot(slotModel.ItemType, out itemStack);
            }

            var currentStack = slotModel.ItemStack;
            if (currentStack?.ItemConfig == null)
            {
                return false;
            }

            var countToTake = Mathf.Max(1, (currentStack.Count + 1) / 2);
            return playerInventory.TryTakeFromSlot(slotModel.ItemType, countToTake, out itemStack);
        }

        private static bool TryTakeFromHoveredTile(
            MouseButtonType button,
            out IInventory hoveredInventory,
            out ItemInInventory itemInInventory,
            out ItemStack itemStack)
        {
            hoveredInventory = null;
            itemInInventory = null;
            itemStack = null;
            if (!InventoryTilePointerHandler.TryGetHovered(out hoveredInventory, out var hoveredTile)
             || !hoveredInventory.TryGet(hoveredTile, out itemInInventory))
            {
                hoveredInventory = null;
                itemInInventory = null;
                return false;
            }

            if (button == MouseButtonType.Left || itemInInventory.Count <= 1)
            {
                itemStack = itemInInventory.ItemStack;
                return true;
            }

            var totalCount = itemInInventory.Count;
            var countToTake = (totalCount + 1) / 2;
            var remainderCount = totalCount - countToTake;
            itemStack = new ItemStack(itemInInventory.ItemConfig, countToTake);
            if (remainderCount > 0)
            {
                hoveredInventory.Add(new ItemStack(itemInInventory.ItemConfig, remainderCount), itemInInventory.Position);
            }

            return true;
        }

        private static void CaptureGrabOffset()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                InventoryPage.Current?.ResetGrabOffset();
                return;
            }

            var interactionPage = GetCurrentInteractionPage();
            if (interactionPage == null || interactionPage.TryCaptureGrabOffset(pointer.position.ReadValue()))
            {
                return;
            }

            interactionPage.ResetGrabOffset();
        }

        private static IInventoryInteractionPage GetCurrentInteractionPage()
        {
            return TradePage.CurrentInteractionPage ?? LootingPage.CurrentInteractionPage ?? InventoryPage.CurrentInteractionPage;
        }

        private static bool IsInventoryInteractionMode(GameMode mode)
        {
            return mode == GameMode.Inventory || mode == GameMode.Looting || mode == GameMode.Trade;
        }

        private void ThrowItem(ItemStack itemStack)
        {
            SpawnItemInWorld(itemStack);
            ClearHand();
        }

        private void RebuildInventoryAndThrowOverflowItems()
        {
            var droppedItems = playerInventory.RebuildInventoryFromCurrentBackpack();
            foreach (var droppedItem in droppedItems)
            {
                SpawnItemInWorld(droppedItem);
            }
        }

        private bool HandleTradeMouseUp(ItemStack itemStack)
        {
            var tradePage = TradePage.Current;
            var pointer = Pointer.current;
            if (tradePage == null || pointer == null)
            {
                return false;
            }

            if (tradePage.CanMoveToPlayerSlot(handSourceInventory, handSourceSlot) && TryAddToHoveredSlot(itemStack))
            {
                tradePage.ConsumeSellOriginIfAny(itemStack, handSourceInventory);
                return true;
            }

            if (InventoryTilePointerHandler.TryGetHovered(out var hoveredInventory, out _)
             && tradePage.CanMoveToInventory(handSourceInventory, handSourceSlot, hoveredInventory))
            {
                var previousCount = itemStack.Count;
                if (TryAddToHoveredTile(itemStack))
                {
                    if (tradePage.IsSellInventory(hoveredInventory))
                    {
                        tradePage.RegisterMoveIntoSell(itemStack.ItemConfig, previousCount, hoveredInventory, handSourceInventory, handSourceSlot, handSourcePosition);
                    }
                    else
                    {
                        tradePage.ConsumeSellOriginIfAny(new ItemStack(itemStack.ItemConfig, previousCount), handSourceInventory);
                    }

                    return true;
                }
            }

            if (tradePage.IsInPlayerSections(pointer.position.ReadValue())
             && tradePage.CanMoveToInventory(handSourceInventory, handSourceSlot, playerInventory)
             && TryStoreOrDrop(itemStack, playerInventory.TryAdd(itemStack)))
            {
                tradePage.ConsumeSellOriginIfAny(itemStack, handSourceInventory);
                return true;
            }

            if (tradePage.IsInTargetSection(pointer.position.ReadValue())
             && tradePage.CanMoveToInventory(handSourceInventory, handSourceSlot, tradePage.GetTargetInventory())
             && TryStoreOrDrop(itemStack, tradePage.GetTargetInventory().TryAdd(itemStack), tradePage.GetTargetInventory()))
            {
                tradePage.ConsumeSellOriginIfAny(itemStack, handSourceInventory);
                return true;
            }

            if (TryReturnToHandSource(itemStack))
            {
                tradePage.ConsumeSellOriginIfAny(itemStack, handSourceInventory);
                ClearHand();
                return true;
            }

            var sourceIsPlayer = tradePage.CanMoveToPlayerSlot(handSourceInventory, handSourceSlot);
            var sourceInventory = sourceIsPlayer ? playerInventory : tradePage.GetTargetInventory();
            if (sourceInventory != null && TryStoreOrDrop(itemStack, sourceInventory.TryAdd(itemStack), sourceInventory))
            {
                tradePage.ConsumeSellOriginIfAny(itemStack, handSourceInventory);
                return true;
            }

            if (tradePage.TryAddToSourceSellInventory(itemStack, handSourceInventory, handSourceSlot, handSourcePosition))
            {
                ClearHand();
                return true;
            }

            return true;
        }

        private bool TryReturnToHandSource(ItemStack itemStack)
        {
            if (handSourceSlot != null && playerInventory.TryPlaceInSlot(handSourceSlot.ItemType, itemStack, out var remainderStack, out var replacedStack))
            {
                if (replacedStack != null)
                {
                    var replacedRemainder = playerInventory.TryAdd(replacedStack);
                    if (replacedRemainder != null)
                    {
                        return false;
                    }
                }

                if (remainderStack != null)
                {
                    return playerInventory.TryAdd(remainderStack) == null;
                }

                return true;
            }

            if (handSourceInventory == null)
            {
                return false;
            }

            ItemStack remainder = itemStack;
            if (handSourceInventory is ITiledInventory tiledInventory)
            {
                var center = handSourcePosition.GetColumn(3);
                var startPosition = new Vector2Int(
                    Mathf.RoundToInt(center.x - (itemStack.ItemConfig.Size.x - 1) * 0.5f),
                    Mathf.RoundToInt(center.y - (itemStack.ItemConfig.Size.y - 1) * 0.5f));
                if (tiledInventory.Tiles.TryGetTile(startPosition.x, startPosition.y, out var tile))
                {
                    remainder = handSourceInventory.TryAdd(itemStack, tile);
                    if (remainder == null)
                    {
                        return true;
                    }
                }
            }

            return handSourceInventory.TryAdd(remainder) == null;
        }

        private void ProcessDelayedBackpackResize()
        {
            if (!backpackResizePendingAfterHandAction)
            {
                return;
            }

            backpackResizePendingAfterHandAction = false;
            var currentBackpack = playerInventory.BackpackSlot.ItemStack;
            if (currentBackpack == backpackTakenFromSlot)
            {
                backpackTakenFromSlot = null;
                return;
            }

            backpackTakenFromSlot = null;
            RebuildInventoryAndThrowOverflowItems();
        }

        private bool TryStoreOrDrop(ItemStack originalStack, ItemStack remainder, IInventory preferredInventory = null)
        {
            if (remainder == null)
            {
                ClearHand();
                return true;
            }

            if (originalStack == null || remainder.Count == originalStack.Count)
            {
                return false;
            }

            var overflow = preferredInventory != null ? preferredInventory.TryAdd(remainder) : remainder;
            if (overflow != null)
            {
                SpawnItemInWorld(overflow);
            }

            ClearHand();
            return true;
        }

        private void SpawnItemInWorld(ItemStack itemStack)
        {
            if (itemStack?.ItemConfig?.HandPrefab == null)
            {
                return;
            }

            var spawnPosition = playerTransform.position + playerTransform.forward * ThrowForwardOffset + Vector3.up * ThrowUpOffset;
            var itemHolder = Object.Instantiate(itemStack.ItemConfig.HandPrefab, spawnPosition, Quaternion.identity);
            itemHolder.SetCount(itemStack.Count);
            itemHolder.CanInteractable = true;

            if (itemHolder.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                var throwDirection = (playerTransform.forward + Vector3.up * 0.35f).normalized;
                rigidbody.AddForce(throwDirection * ThrowForce, ForceMode.Impulse);
            }
        }

        public void Drop(ItemStack itemStack)
        {
            SpawnItemInWorld(itemStack);
        }

        public bool TryUseFromInventory(ItemStack itemStack)
        {
            if (itemStack?.ItemConfig == null
             || !playerInventory.TryPlaceInSlot(itemStack.ItemConfig.ItemType, itemStack, out var remainderStack, out var replacedStack))
            {
                return false;
            }

            if (itemStack.ItemConfig.ItemType == ItemType.Backpack)
            {
                RebuildInventoryAndThrowOverflowItems();
            }

            if (replacedStack != null)
            {
                var replacedRemainder = playerInventory.TryAdd(replacedStack);
                if (replacedRemainder != null)
                {
                    SpawnItemInWorld(replacedRemainder);
                }
            }

            if (remainderStack != null)
            {
                var remainderAfterInventory = playerInventory.TryAdd(remainderStack);
                if (remainderAfterInventory != null)
                {
                    SpawnItemInWorld(remainderAfterInventory);
                }
            }

            return true;
        }
    }
}
