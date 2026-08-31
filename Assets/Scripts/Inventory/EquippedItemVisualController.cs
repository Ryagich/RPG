using System;
using System.Collections.Generic;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Inventory
{
    public class EquippedItemVisualController : IStartable, IDisposable
    {
        private readonly IEquipmentInventory inventory;
        private readonly CharacterVisualRoot characterVisualRoot;
        private readonly CharacterDefaultVisualConfig defaultVisualConfig;
        private readonly IDisposable inventoryChangedSubscription;
        private readonly HashSet<string> missingBindingWarnings = new();
        private readonly System.Collections.Generic.Dictionary<BodyPart, string> desiredVisualsByBodyPart = new();

        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastFaceItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastHandsItemConfig;
        private ItemConfig lastArmsSlotItemConfig;
        private ItemConfig lastLegsItemConfig;
        private ItemConfig lastHipsItemConfig;
        private ItemConfig lastBackpackItemConfig;

        public EquippedItemVisualController(
            IEquipmentInventory inventory,
            CharacterVisualRoot characterVisualRoot,
            CharacterDefaultVisualConfig defaultVisualConfig = null)
        {
            this.inventory = inventory;
            this.characterVisualRoot = characterVisualRoot;
            this.defaultVisualConfig = defaultVisualConfig;
            inventoryChangedSubscription = inventory.Changed.Subscribe(_ => RefreshVisuals());
        }

        public void Start()
        {
            RefreshVisuals(force: true);
        }

        public void Dispose()
        {
            inventoryChangedSubscription.Dispose();
        }

        private void RefreshVisuals(bool force = false)
        {
            if (!force
                && lastHelmItemConfig == inventory.HelmSlot.ItemConfig
                && lastFaceItemConfig == inventory.FaceSlot.ItemConfig
                && lastBodyItemConfig == inventory.BodySlot.ItemConfig
                && lastHandsItemConfig == inventory.HandsSlot.ItemConfig
                && lastArmsSlotItemConfig == inventory.ArmsSlot.ItemConfig
                && lastLegsItemConfig == inventory.LegsSlot.ItemConfig
                && lastHipsItemConfig == inventory.HipsSlot.ItemConfig
                && lastBackpackItemConfig == inventory.BackpackSlot.ItemConfig)
            {
                return;
            }

            desiredVisualsByBodyPart.Clear();
            ApplyDefaultVisuals();
            ApplySlotVisuals(inventory.HelmSlot, "Helm");
            ApplySlotVisuals(inventory.FaceSlot, "Face");
            ApplySlotVisuals(inventory.BodySlot, "Body");
            ApplySlotVisuals(inventory.HandsSlot, "Hands");
            ApplySlotVisuals(inventory.ArmsSlot, "ArmsSlot");
            ApplySlotVisuals(inventory.LegsSlot, "Legs");
            ApplySlotVisuals(inventory.HipsSlot, "Hips");
            ApplySlotVisuals(inventory.BackpackSlot, "Backpack");

            characterVisualRoot.ApplyVisuals(desiredVisualsByBodyPart);
            lastHelmItemConfig = inventory.HelmSlot.ItemConfig;
            lastFaceItemConfig = inventory.FaceSlot.ItemConfig;
            lastBodyItemConfig = inventory.BodySlot.ItemConfig;
            lastHandsItemConfig = inventory.HandsSlot.ItemConfig;
            lastArmsSlotItemConfig = inventory.ArmsSlot.ItemConfig;
            lastLegsItemConfig = inventory.LegsSlot.ItemConfig;
            lastHipsItemConfig = inventory.HipsSlot.ItemConfig;
            lastBackpackItemConfig = inventory.BackpackSlot.ItemConfig;
        }

        private void ApplyDefaultVisuals()
        {
            if (defaultVisualConfig == null)
            {
                return;
            }

            foreach (var visual in defaultVisualConfig.DefaultVisuals)
            {
                ApplyBodyPartVisual(visual?.BodyPart ?? BodyPart.None, visual?.VisualName, "DefaultVisualConfig");
            }
        }

        private void ApplySlotVisuals(SlotModel slotModel, string source)
        {
            var itemConfig = slotModel?.ItemConfig;
            if (itemConfig == null)
            {
                return;
            }

            foreach (var visual in itemConfig.EquippedVisuals)
            {
                ApplyBodyPartVisual(visual?.BodyPart ?? BodyPart.None, visual?.VisualName, $"{source}:{itemConfig.Id}");
            }
        }

        private void ApplyBodyPartVisual(BodyPart bodyPart, string visualName, string source)
        {
            if (bodyPart == BodyPart.None)
            {
                return;
            }

            // Explicit empty value means "this body part must be empty".
            // This is used both by defaults and item overrides.
            desiredVisualsByBodyPart[bodyPart] = string.IsNullOrWhiteSpace(visualName)
                ? string.Empty
                : visualName;

            if (string.IsNullOrWhiteSpace(visualName) || characterVisualRoot.HasVisual(bodyPart, visualName))
            {
                return;
            }

            var warningKey = $"{source}:{bodyPart}:{visualName}";
            if (!missingBindingWarnings.Add(warningKey))
            {
                return;
            }

            Debug.LogWarning($"Missing character visual '{visualName}' for body part {bodyPart}. Source: {source}.", null);
        }
    }
}
