using System.Collections.Generic;
using Inventory.Item;
using UnityEngine;

namespace NPC
{
    /// <summary>Collects nearby world items for the NPC item-pickup system.</summary>
    [DisallowMultipleComponent]
    public sealed class NpcItemVisionSensor : VisionSensorBase
    {
        private readonly HashSet<ItemHolder> itemCandidates = new();

        public IEnumerable<ItemHolder> ItemCandidates => itemCandidates;
        public bool HasItemCandidates => itemCandidates.Count > 0;

        protected override void OnVisionTriggerEnter(Collider other)
        {
            var itemHolder = other != null ? other.GetComponentInParent<ItemHolder>() : null;
            if (itemHolder != null && itemHolder.CanInteractable)
            {
                itemCandidates.Add(itemHolder);
            }
        }

        protected override void OnVisionTriggerExit(Collider other)
        {
            var itemHolder = other != null ? other.GetComponentInParent<ItemHolder>() : null;
            if (itemHolder != null)
            {
                itemCandidates.Remove(itemHolder);
            }
        }

        public void PruneInvalidCandidates() =>
            itemCandidates.RemoveWhere(item => item == null || !item.CanInteractable);
    }
}
