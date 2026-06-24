using UI.Inventory;
using UnityEngine;

namespace UI.UIElements
{
    public class LeftAnotherInventory : MonoBehaviour
    {
        [field: SerializeField] public InventoryView InventoryView { get; private set; }
        [field: SerializeField] public InfoAboutInventory InfoAboutInventory { get; private set; }
        [field: SerializeField] public InfoAboutPlayer InfoAboutPlayer { get; private set; }
    }
}
