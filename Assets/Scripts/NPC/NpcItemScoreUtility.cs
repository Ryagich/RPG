using Inventory.Item;
using UnityEngine;

namespace NPC
{
    public static class NpcItemScoreUtility
    {
        public static float Calculate(ItemStack itemStack, NpcItemPickupConfig config)
        {
            if (itemStack?.ItemConfig == null)
            {
                return 0f;
            }

            var size = itemStack.Size;
            var area = Mathf.Max(1, size.x * size.y);
            var weightPenalty = config != null ? config.WeightPenalty : 0.25f;
            var sizePenalty = config != null ? config.SizePenalty : 0.1f;
            var denominator = 1f + Mathf.Max(0f, weightPenalty) * itemStack.TotalWeight + Mathf.Max(0f, sizePenalty) * area;
            return itemStack.TotalPrice / Mathf.Max(0.01f, denominator);
        }
    }
}
