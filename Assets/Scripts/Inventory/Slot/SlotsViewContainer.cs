using UnityEngine;

namespace Inventory.Slot
{
    public class SlotsViewContainer : MonoBehaviour
    {
        [field: SerializeField] public SlotView HeadSlot { get; private set; }
        [field: SerializeField] public SlotView BodySlot { get; private set; }
        [field: SerializeField] public SlotView BackpackSlot { get; private set; }
    }
}