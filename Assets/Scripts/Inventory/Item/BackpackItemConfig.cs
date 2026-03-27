using UnityEngine;

namespace Inventory.Item
{
    [CreateAssetMenu(fileName = "BackpackItemConfig", menuName = "configs/Inventory/BackpackItemConfig")]
    public class BackpackItemConfig : ItemConfig
    {
        [field: SerializeField] public new Vector2Int BackpackSize { get; private set; } = new(7, 2);
    }
}