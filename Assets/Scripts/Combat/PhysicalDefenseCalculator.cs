using Inventory.Inventories;
using Inventory.Item;
using UnityEngine;

namespace Combat
{
    public static class PhysicalDefenseCalculator
    {
        private static readonly DamageBodyPart[] ProtectedBodyParts =
        {
            DamageBodyPart.Head,
            DamageBodyPart.Body,
            DamageBodyPart.Arms,
            DamageBodyPart.Hands,
            DamageBodyPart.Hips,
            DamageBodyPart.Legs,
            DamageBodyPart.Feet
        };

        public static float CalculateEffective(IEquipmentInventory inventory)
        {
            return CalculateEffective(inventory, ItemType.None, null);
        }

        public static float CalculateEffective(IEquipmentInventory inventory, ItemType overrideSlotType, ItemConfig overrideItemConfig)
        {
            if (inventory == null)
            {
                return 0f;
            }

            var weightedProtection = 0f;
            var totalWeight = 0f;

            foreach (var bodyPart in ProtectedBodyParts)
            {
                var weight = DamageBodyPartUtility.GetDefaultDamageMultiplier(bodyPart);
                weightedProtection += ResolveProtection(inventory, bodyPart, overrideSlotType, overrideItemConfig) * weight;
                totalWeight += weight;
            }

            return totalWeight <= 0f
                ? 0f
                : Mathf.Clamp01(weightedProtection / totalWeight);
        }

        public static float ResolveProtection(IEquipmentInventory inventory, DamageBodyPart bodyPart)
        {
            return ResolveProtection(inventory, bodyPart, ItemType.None, null);
        }

        private static float ResolveProtection(
            IEquipmentInventory inventory,
            DamageBodyPart bodyPart,
            ItemType overrideSlotType,
            ItemConfig overrideItemConfig)
        {
            if (inventory == null || bodyPart == DamageBodyPart.None)
            {
                return 0f;
            }

            var protection =
                GetSlotProtection(inventory, ItemType.Helm, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(inventory, ItemType.Face, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(inventory, ItemType.Body, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(inventory, ItemType.Arms, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(inventory, ItemType.Hands, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(inventory, ItemType.Hips, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(inventory, ItemType.Legs, bodyPart, overrideSlotType, overrideItemConfig);

            return Mathf.Clamp01(protection);
        }

        private static float GetSlotProtection(
            IEquipmentInventory inventory,
            ItemType slotType,
            DamageBodyPart bodyPart,
            ItemType overrideSlotType,
            ItemConfig overrideItemConfig)
        {
            var itemConfig = overrideSlotType == slotType
                ? overrideItemConfig
                : GetEquippedItemConfig(inventory, slotType);

            if (itemConfig == null || !DamageBodyPartUtility.IsProtectedBy(itemConfig.ItemType, bodyPart))
            {
                return 0f;
            }

            return Mathf.Clamp01(itemConfig.PhysicalDefense);
        }

        private static ItemConfig GetEquippedItemConfig(IEquipmentInventory inventory, ItemType slotType)
        {
            return slotType switch
            {
                ItemType.Helm => inventory.HelmSlot.ItemConfig,
                ItemType.Face => inventory.FaceSlot.ItemConfig,
                ItemType.Body => inventory.BodySlot.ItemConfig,
                ItemType.Arms => inventory.ArmsSlot.ItemConfig,
                ItemType.Hands => inventory.HandsSlot.ItemConfig,
                ItemType.Hips => inventory.HipsSlot.ItemConfig,
                ItemType.Legs => inventory.LegsSlot.ItemConfig,
                _ => null
            };
        }
    }
}
