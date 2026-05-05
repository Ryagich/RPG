using System;
using UnityEngine;

namespace Inventory.Item
{
    [Serializable]
    public class EquippedItemVisual
    {
        [field: SerializeField] public BodyPart BodyPart { get; private set; }
        [field: SerializeField] public string VisualName { get; private set; }
    }
}
