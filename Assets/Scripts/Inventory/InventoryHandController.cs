using System.Collections.Generic;
using GameModes;
using Inventory.Item;
using MessagePipe;
using Messages;
using UI.Inventory;
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
            if (gameModesController.GameMode != GameMode.Inventory || HasItemInHand()
             || !InventoryTilePointerHandler.TryGetHovered(out var hoveredInventory, out var hoveredTile)
             || !hoveredInventory.TryGet(hoveredTile, out var itemInInventory))
            {
                return;
            }
            playerInventory.HandSlot.Value = new Slot
                                             {
                                                 ItemConfig = itemInInventory.ItemConfig,
                                                 ItemType = itemInInventory.ItemConfig.ItemType
                                             };
        }

        private void OnMouseUp(MouseUp _)
        {
            if (gameModesController.GameMode != GameMode.Inventory || !HasItemInHand())
            {
                return;
            }
            var itemConfig = playerInventory.HandSlot.Value.ItemConfig;

            if (TryAddToHoveredTile(itemConfig))
            {
                ClearHand();
                return;
            }
            
            if (IsPointerOverInventory() && playerInventory.TryAdd(itemConfig))
            {
                ClearHand();
                return;
            }
            ThrowItem(itemConfig);
        }
        
        private static bool IsPointerOverInventory()
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
                if (raycastResult.gameObject.GetComponentInParent<InventoryView>() != null)
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
            if (!InventoryTilePointerHandler.TryGetHovered(out var hoveredInventory, out var hoveredTile))
            {
                return false;
            }

            return hoveredInventory.TryAdd(itemConfig, hoveredTile);
        }

        private bool HasItemInHand() => playerInventory.HandSlot.Value?.ItemConfig != null;
        private void ClearHand() => playerInventory.HandSlot.Value = new Slot();

        private void ThrowItem(ItemConfig itemConfig)
        {
            var spawnPosition = playerTransform.position + playerTransform.forward * ThrowForwardOffset + Vector3.up * ThrowUpOffset;
            var itemHolder = Object.Instantiate(itemConfig.HandPrefab, spawnPosition, Quaternion.identity);
            itemHolder.CanInteractable = true;

            if (itemHolder.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                var throwDirection = (playerTransform.forward + Vector3.up * 0.35f).normalized;
                rigidbody.AddForce(throwDirection * ThrowForce, ForceMode.Impulse);
            }

            ClearHand();
        }
    }
}
