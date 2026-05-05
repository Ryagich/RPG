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
        private readonly PlayerInventory playerInventory;
        private readonly CharacterVisualRoot characterVisualRoot;
        private readonly IDisposable inventoryChangedSubscription;
        private readonly HashSet<string> missingBindingWarnings = new();
        private readonly System.Collections.Generic.Dictionary<BodyPart, string> desiredVisualsByBodyPart = new();

        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastBackpackItemConfig;

        public EquippedItemVisualController(PlayerInventory playerInventory, CharacterVisualRoot characterVisualRoot)
        {
            this.playerInventory = playerInventory;
            this.characterVisualRoot = characterVisualRoot;
            inventoryChangedSubscription = playerInventory.Changed.Subscribe(_ => RefreshVisuals());
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
                && lastHelmItemConfig == playerInventory.HelmSlot.ItemConfig
                && lastBodyItemConfig == playerInventory.BodySlot.ItemConfig
                && lastBackpackItemConfig == playerInventory.BackpackSlot.ItemConfig)
            {
                return;
            }

            desiredVisualsByBodyPart.Clear();
            ApplyDefaultVisuals();
            ApplySlotVisuals(playerInventory.HelmSlot, "Helm");
            ApplySlotVisuals(playerInventory.BodySlot, "Body");
            ApplySlotVisuals(playerInventory.BackpackSlot, "Backpack");

            characterVisualRoot.ApplyVisuals(desiredVisualsByBodyPart);
            lastHelmItemConfig = playerInventory.HelmSlot.ItemConfig;
            lastBodyItemConfig = playerInventory.BodySlot.ItemConfig;
            lastBackpackItemConfig = playerInventory.BackpackSlot.ItemConfig;
        }

        private void ApplyDefaultVisuals()
        {
            var defaultVisualConfig = characterVisualRoot.DefaultVisualConfig;
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
