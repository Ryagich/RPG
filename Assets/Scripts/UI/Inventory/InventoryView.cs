using UnityEngine;

namespace UI.Inventory
{
    public class InventoryView : MonoBehaviour
    {
        [field: SerializeField] public RectTransform ContentForTiles { get; private set; }
        [field: SerializeField] public RectTransform ContentForItems { get; private set; }
    }
}