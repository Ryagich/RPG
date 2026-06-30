using Inventory.Item;

namespace Combat
{
    public static class DamageBodyPartUtility
    {
        public static float GetDefaultDamageMultiplier(DamageBodyPart bodyPart)
        {
            return bodyPart switch
            {
                DamageBodyPart.Head => 2f,
                DamageBodyPart.Body => 1f,
                DamageBodyPart.Arms => 0.5f,
                DamageBodyPart.Hands => 0.5f,
                DamageBodyPart.Hips => 0.8f,
                DamageBodyPart.Legs => 0.7f,
                DamageBodyPart.Feet => 0.5f,
                _ => 1f
            };
        }

        public static bool IsProtectedBy(ItemType itemType, DamageBodyPart bodyPart)
        {
            return itemType switch
            {
                ItemType.Helm => bodyPart == DamageBodyPart.Head,
                ItemType.Face => bodyPart == DamageBodyPart.Head,
                ItemType.Body => bodyPart == DamageBodyPart.Body,
                ItemType.Arms => bodyPart == DamageBodyPart.Arms,
                ItemType.Hands => bodyPart == DamageBodyPart.Hands,
                ItemType.Hips => bodyPart == DamageBodyPart.Hips,
                ItemType.Legs => bodyPart is DamageBodyPart.Legs or DamageBodyPart.Feet,
                _ => false
            };
        }
    }
}
