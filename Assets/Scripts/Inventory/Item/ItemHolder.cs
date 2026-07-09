using System;
using UnityEngine;

namespace Inventory.Item
{
    public class ItemHolder : MonoBehaviour
    {
        public event Action<ItemHolder> Destroyed;

        [field: SerializeField] public ItemConfig Config { get; private set; } = null!;
        [field: SerializeField, Min(1)] public int Count { get; private set; } = 1;

        public bool CanInteractable = true;

        public ItemStack GetItemStack()
        {
            return Config == null ? null : new ItemStack(Config, Count);
        }

        public void Initialize(ItemConfig config, int count = 1)
        {
            Config = config;
            SetCount(count);
        }

        public void SetCount(int count)
        {
            Count = Mathf.Max(1, count);
        }
        
        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
        }
    }
}
