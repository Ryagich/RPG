using UnityEngine;

namespace Inventory
{
    [CreateAssetMenu(fileName = "InventoryConfig", menuName = "configs/Inventory/InventoryConfig")]
    public class InventoryConfig : ScriptableObject
    {
        [field: SerializeField] public Vector2Int Size { get; private set; } = new(7, 2);
        [field: SerializeField] public float DefaultMaxWeight { get; private set; } = 20.0f;
        [field: SerializeField, Min(0f)] public float WeightAffectsMovementPercent { get; private set; } = 0.5f;
        [field: SerializeField, Min(0f)] public float WeightBlocksMovementPercent { get; private set; } = 1f;
    }
}
