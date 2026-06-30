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

        public static float CalculateEffective(PlayerInventory playerInventory)
        {
            return CalculateEffective(playerInventory, ItemType.None, null);
        }

        public static float CalculateEffective(PlayerInventory playerInventory, ItemType overrideSlotType, ItemConfig overrideItemConfig)
        {
            if (playerInventory == null)
            {
                return 0f;
            }

            var weightedProtection = 0f;
            var totalWeight = 0f;

            foreach (var bodyPart in ProtectedBodyParts)
            {
                var weight = DamageBodyPartUtility.GetDefaultDamageMultiplier(bodyPart);
                weightedProtection += ResolveProtection(playerInventory, bodyPart, overrideSlotType, overrideItemConfig) * weight;
                totalWeight += weight;
            }

            return totalWeight <= 0f
                ? 0f
                : Mathf.Clamp01(weightedProtection / totalWeight);
        }

        public static float ResolveProtection(PlayerInventory playerInventory, DamageBodyPart bodyPart)
        {
            return ResolveProtection(playerInventory, bodyPart, ItemType.None, null);
        }

        private static float ResolveProtection(
            PlayerInventory playerInventory,
            DamageBodyPart bodyPart,
            ItemType overrideSlotType,
            ItemConfig overrideItemConfig)
        {
            if (playerInventory == null || bodyPart == DamageBodyPart.None)
            {
                return 0f;
            }

            var protection =
                GetSlotProtection(playerInventory, ItemType.Helm, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(playerInventory, ItemType.Face, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(playerInventory, ItemType.Body, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(playerInventory, ItemType.Arms, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(playerInventory, ItemType.Hands, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(playerInventory, ItemType.Hips, bodyPart, overrideSlotType, overrideItemConfig)
              + GetSlotProtection(playerInventory, ItemType.Legs, bodyPart, overrideSlotType, overrideItemConfig);

            return Mathf.Clamp01(protection);
        }

        private static float GetSlotProtection(
            PlayerInventory playerInventory,
            ItemType slotType,
            DamageBodyPart bodyPart,
            ItemType overrideSlotType,
            ItemConfig overrideItemConfig)
        {
            var itemConfig = overrideSlotType == slotType
                ? overrideItemConfig
                : GetEquippedItemConfig(playerInventory, slotType);

            if (itemConfig == null || !DamageBodyPartUtility.IsProtectedBy(itemConfig.ItemType, bodyPart))
            {
                return 0f;
            }

            return Mathf.Clamp01(itemConfig.PhysicalDefense);
        }

        private static ItemConfig GetEquippedItemConfig(PlayerInventory playerInventory, ItemType slotType)
        {
            return slotType switch
            {
                ItemType.Helm => playerInventory.HelmSlot.ItemConfig,
                ItemType.Face => playerInventory.FaceSlot.ItemConfig,
                ItemType.Body => playerInventory.BodySlot.ItemConfig,
                ItemType.Arms => playerInventory.ArmsSlot.ItemConfig,
                ItemType.Hands => playerInventory.HandsSlot.ItemConfig,
                ItemType.Hips => playerInventory.HipsSlot.ItemConfig,
                ItemType.Legs => playerInventory.LegsSlot.ItemConfig,
                _ => null
            };
        }
    }
}
