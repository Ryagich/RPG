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

        private static readonly ItemType[] ArmorSlots =
        {
            ItemType.Helm,
            ItemType.Face,
            ItemType.Body,
            ItemType.Arms,
            ItemType.Hands,
            ItemType.Hips,
            ItemType.Legs
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
                weightedProtection += ResolveProtection(inventory, bodyPart, overrideSlotType, overrideItemConfig, out _) * weight;
                totalWeight += weight;
            }

            return totalWeight <= 0f
                ? 0f
                : Mathf.Clamp01(weightedProtection / totalWeight);
        }

        public static float ResolveProtection(IEquipmentInventory inventory, DamageBodyPart bodyPart)
        {
            return ResolveProtection(inventory, bodyPart, out _);
        }

        public static float ResolveProtection(IEquipmentInventory inventory, DamageBodyPart bodyPart, out ItemConfig primaryArmor)
        {
            return ResolveProtection(inventory, bodyPart, ItemType.None, null, out primaryArmor);
        }

        private static float ResolveProtection(
            IEquipmentInventory inventory,
            DamageBodyPart bodyPart,
            ItemType overrideSlotType,
            ItemConfig overrideItemConfig,
            out ItemConfig primaryArmor)
        {
            primaryArmor = null;
            if (inventory == null || bodyPart == DamageBodyPart.None)
            {
                return 0f;
            }

            var protection = 0f;
            var primaryProtection = 0f;
            foreach (var slotType in ArmorSlots)
            {
                var itemConfig = overrideSlotType == slotType
                    ? overrideItemConfig
                    : GetEquippedItemConfig(inventory, slotType);
                if (itemConfig == null || !DamageBodyPartUtility.IsProtectedBy(itemConfig.ItemType, bodyPart))
                {
                    continue;
                }

                var slotProtection = Mathf.Clamp01(itemConfig.PhysicalDefense);
                protection += slotProtection;
                // Keep equipment slot order as the tie-breaker when protection is equal.
                if (slotProtection > primaryProtection)
                {
                    primaryProtection = slotProtection;
                    primaryArmor = itemConfig;
                }
            }

            return Mathf.Clamp01(protection);
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
