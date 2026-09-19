using System;
using Inventory;
using Inventory.Item;
using UnityEngine;

namespace NPC
{
    [Obsolete("Use Inventory.CharacterWorldItemDropper. Player and NPC share character item drop systems.")]
    public sealed class NpcWorldItemDropper
    {
        private readonly CharacterWorldItemDropper inner;

        public NpcWorldItemDropper(Transform ownerTransform, IWorldItemDropObserver worldItemDropObserver)
        {
            inner = new CharacterWorldItemDropper(ownerTransform, worldItemDropObserver);
        }

        public void Drop(ItemStack itemStack) => inner.Drop(itemStack);

        public void DropAt(ItemStack itemStack, Vector3 position, Quaternion rotation)
        {
            inner.DropAt(itemStack, position, rotation);
        }
    }
}
