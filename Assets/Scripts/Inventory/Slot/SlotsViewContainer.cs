using Stats;
using UnityEngine;

namespace Inventory.Slot
{
    public class SlotsViewContainer : MonoBehaviour
    {
        [field: SerializeField] public SlotView HeadSlot { get; private set; }
        [field: SerializeField] public SlotView BodySlot { get; private set; }
        [field: SerializeField] public SlotView BackpackSlot { get; private set; }
        
        [field: SerializeField] public SlotView FastSlot1 { get; private set; }
        [field: SerializeField] public SlotView FastSlot2 { get; private set; }
        [field: SerializeField] public SlotView FastSlot3 { get; private set; }
        [field: SerializeField] public SlotView FastSlot4 { get; private set; }

        [field: SerializeField] public StatHolder PhysicalDefenseStat { get; private set; } 
        [field: SerializeField] public StatHolder TemperatureDefenseStat { get; private set; }
        [field: SerializeField] public StatHolder PsiDefenseStat { get; private set; }
        [field: SerializeField] public StatHolder MagicDefenseStat { get; private set; }
    }
}