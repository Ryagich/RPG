using System;
using UnityEngine;

namespace Inventory.Item
{
    public class ItemHolder : MonoBehaviour
    {
        public event Action<ItemHolder> Destroyed;

        [field: SerializeField] public ItemConfig Config { get; private set; } = null!;

        public bool CanInteractable;
        
        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
        }
    }
}