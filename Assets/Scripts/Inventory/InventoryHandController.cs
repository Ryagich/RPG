using System.Collections.Generic;
using GameModes;
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
        private ItemConfig backpackTakenFromSlot;

        public InventoryHandController
            (
                PlayerInventory playerInventory,
                Transform playerTransform,
                GameModesController gameModesController,
                ISubscriber<MouseDown> mouseDownSubscriber,
                ISubscriber<MouseUp> mouseUpSubscriber,
                ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber
            )
        {
            this.playerInventory = playerInventory;
            this.playerTransform = playerTransform;
            this.gameModesController = gameModesController;

            mouseDownSubscriber.Subscribe(OnMouseDown);
            mouseUpSubscriber.Subscribe(OnMouseUp);
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
        }

        public void Start() { }

        private void OnMouseDown(MouseDown _)
        {
            CaptureGrabOffset();

            if (gameModesController.GameMode != GameMode.Inventory || HasItemInHand())
            {
                return;
            }

            var inventoryPage = InventoryPage.Current;
            var pointer = Pointer.current;
            if (inventoryPage != null && pointer != null
                                      && inventoryPage.TryGetHoveredSlot(pointer.position.ReadValue(), out var slotModel)
                                      && playerInventory.TryTakeFromSlot(slotModel.ItemType, out var slotItemConfig))
            {
                if (slotModel.ItemType == ItemType.Backpack)
                {
                    backpackResizePendingAfterHandAction = true;
                    backpackTakenFromSlot = slotItemConfig;
                }

                playerInventory.HandSlot.Value = new SlotModel(slotItemConfig.ItemType, slotItemConfig);
                return;
            }

            if (!InventoryTilePointerHandler.TryGetHovered(out var hoveredInventory, out var hoveredTile)
             || !hoveredInventory.TryGet(hoveredTile, out var itemInInventory))
            {
                return;
            }

            playerInventory.HandSlot.Value = new SlotModel(itemInInventory.ItemConfig.ItemType, itemInInventory.ItemConfig);
        }

        private void OnMouseUp(MouseUp _)
        {
            if (gameModesController.GameMode != GameMode.Inventory || !HasItemInHand())
            {
                return;
            }
            var itemConfig = playerInventory.HandSlot.Value.ItemConfig;

            if (TryAddToHoveredSlot(itemConfig))
            {
                ClearHand();
                return;
            }

            if (TryAddToHoveredTile(itemConfig))
            {
                ClearHand();
                return;
            }
            
            if (IsPointerOverSlot() && playerInventory.TryAdd(itemConfig))
            {
                ClearHand();
                return;
            }
            
            if (IsPointerOverInventory() && (playerInventory.TryAddToGrid(itemConfig) || playerInventory.TryAdd(itemConfig)))
            {
                ClearHand();
                return;
            }
            
            var inventoryPage = InventoryPage.Current;
            var pointer = Pointer.current;
            if (inventoryPage != null
             && pointer != null
             && inventoryPage.IsInRightOrCenterSection(pointer.position.ReadValue())
             && playerInventory.TryAdd(itemConfig))
            {
                ClearHand();
                return;
            }
            
            ThrowItem(itemConfig);
        }
        private bool TryAddToHoveredSlot(ItemConfig itemConfig)
        {
            var inventoryPage = InventoryPage.Current;
            var pointer = Pointer.current;
            if (inventoryPage == null || pointer == null
                                      || !inventoryPage.TryGetHoveredSlot(pointer.position.ReadValue(), out var slotModel)
                                      || !playerInventory.TryPlaceInSlot(slotModel.ItemType, itemConfig, out var replacedItemConfig))
            {
                return false;
            }

            if (replacedItemConfig == null)
            {
                if (slotModel.ItemType == ItemType.Backpack)
                {
                    RebuildInventoryAndThrowOverflowItems();
                }

                return true;
            }

            if (slotModel.ItemType == ItemType.Backpack)
            {
                RebuildInventoryAndThrowOverflowItems();
            }

            if (playerInventory.TryAddToGrid(replacedItemConfig))
            {
                return true;
            }

            ThrowItem(replacedItemConfig);
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
            if (msg.GameMode == GameMode.Inventory || !HasItemInHand())
            {
                return;
            }
            var itemConfig = playerInventory.HandSlot.Value.ItemConfig;

            if (playerInventory.TryAdd(itemConfig))
            {
                ClearHand();
                return;
            }
            ThrowItem(itemConfig);
        }

        private bool TryAddToHoveredTile(ItemConfig itemConfig)
        {
            if (!InventoryTilePointerHandler.TryGetHovered(out var hoveredInventory, out _))
            {
                return false;
            }

            var inventoryPage = InventoryPage.Current;
            var pointer = Pointer.current;
            if (inventoryPage == null || pointer == null)
            {
                return false;
            }

            if (!inventoryPage.TryGetPlacementTile(pointer.position.ReadValue(), out var placementTile))
            {
                return false;
            }

            return hoveredInventory.TryAdd(itemConfig, placementTile);
        }


        private bool HasItemInHand() => playerInventory.HandSlot.Value?.ItemConfig != null;
       
        private void ClearHand()
        {
            playerInventory.HandSlot.Value = new SlotModel(ItemType.None, null);
            InventoryPage.Current?.ResetGrabOffset();
            ProcessDelayedBackpackResize();
        }

        private static void CaptureGrabOffset()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                InventoryPage.Current?.ResetGrabOffset();
                return;
            }

            var inventoryPage = InventoryPage.Current;
            if (inventoryPage == null || inventoryPage.TryCaptureGrabOffset(pointer.position.ReadValue()))
            {
                return;
            }

            inventoryPage.ResetGrabOffset();
        }
        
        private void ThrowItem(ItemConfig itemConfig)
        {
            SpawnItemInWorld(itemConfig);
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

        private void ProcessDelayedBackpackResize()
        {
            if (!backpackResizePendingAfterHandAction)
            {
                return;
            }

            backpackResizePendingAfterHandAction = false;
            var currentBackpack = playerInventory.BackpackSlot.ItemConfig;
            if (currentBackpack == backpackTakenFromSlot)
            {
                backpackTakenFromSlot = null;
                return;
            }

            backpackTakenFromSlot = null;
            RebuildInventoryAndThrowOverflowItems();
        }

        private void SpawnItemInWorld(ItemConfig itemConfig)
        {
            var spawnPosition = playerTransform.position + playerTransform.forward * ThrowForwardOffset + Vector3.up * ThrowUpOffset;
            var itemHolder = Object.Instantiate(itemConfig.HandPrefab, spawnPosition, Quaternion.identity);
            itemHolder.CanInteractable = true;

            if (itemHolder.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                var throwDirection = (playerTransform.forward + Vector3.up * 0.35f).normalized;
                rigidbody.AddForce(throwDirection * ThrowForce, ForceMode.Impulse);
            }
        }
    }
}
