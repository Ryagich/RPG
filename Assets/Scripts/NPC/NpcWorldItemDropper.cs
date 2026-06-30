using Inventory.Item;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NPC
{
    public sealed class NpcWorldItemDropper
    {
        private const float DropForwardOffset = 1.25f;
        private const float DropUpOffset = 1f;
        private const float DropForce = 2.5f;

        private readonly Transform ownerTransform;

        public NpcWorldItemDropper(Transform ownerTransform)
        {
            this.ownerTransform = ownerTransform;
        }

        public void Drop(ItemStack itemStack)
        {
            if (itemStack?.ItemConfig?.HandPrefab == null || ownerTransform == null)
            {
                return;
            }

            var forward = ownerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            var spawnPosition = ownerTransform.position + forward * DropForwardOffset + Vector3.up * DropUpOffset;
            var itemHolder = Object.Instantiate(itemStack.ItemConfig.HandPrefab, spawnPosition, Quaternion.identity);
            itemHolder.SetCount(itemStack.Count);
            itemHolder.CanInteractable = true;

            if (itemHolder.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                var throwDirection = (forward + Vector3.up * 0.35f).normalized;
                rigidbody.AddForce(throwDirection * DropForce, ForceMode.Impulse);
            }
        }

        public void DropAt(ItemStack itemStack, Vector3 position, Quaternion rotation)
        {
            if (itemStack?.ItemConfig?.HandPrefab == null)
            {
                return;
            }

            var itemHolder = Object.Instantiate(itemStack.ItemConfig.HandPrefab, position, rotation);
            itemHolder.SetCount(itemStack.Count);
            itemHolder.CanInteractable = true;
        }
    }
}
