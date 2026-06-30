using System.Collections.Generic;
using Inventory.Item;
using Inventory.Slot;

namespace NPC
{
    public sealed class NpcItemPickupPlan
    {
        public ItemHolder ItemHolder { get; }
        public ItemStack ItemStack { get; }
        public SlotModel TargetSlot { get; }
        public IReadOnlyList<NpcInventoryDropSource> DropSources { get; }
        public float Gain { get; }
        public float CandidateScore { get; }

        public bool UseSlot => TargetSlot != null;

        public NpcItemPickupPlan(
            ItemHolder itemHolder,
            ItemStack itemStack,
            SlotModel targetSlot,
            IReadOnlyList<NpcInventoryDropSource> dropSources,
            float gain,
            float candidateScore)
        {
            ItemHolder = itemHolder;
            ItemStack = itemStack;
            TargetSlot = targetSlot;
            DropSources = dropSources ?? System.Array.Empty<NpcInventoryDropSource>();
            Gain = gain;
            CandidateScore = candidateScore;
        }
    }
}
