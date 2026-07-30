using System.Collections.Generic;
using UnityEngine;

namespace Inventory.Item
{
    [CreateAssetMenu(fileName = "ItemSetConfig", menuName = "configs/Inventory/Item Set Config")]
    public sealed class ItemSetConfig : ScriptableObject
    {
        [field: SerializeField] public List<ItemConfig> ItemConfigs { get; private set; } = new();
    }
}
