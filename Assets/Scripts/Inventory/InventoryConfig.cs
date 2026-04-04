using UnityEngine;

namespace Inventory
{
    [CreateAssetMenu(fileName = "InventoryConfig", menuName = "configs/Inventory/InventoryConfig")]
    public class InventoryConfig : ScriptableObject
    {
        [field: SerializeField] public Vector2Int Size { get; private set; } = new(7, 2);
        [field: SerializeField] public float DefaultMaxWeight { get; private set; } = 20.0f;
    }
}