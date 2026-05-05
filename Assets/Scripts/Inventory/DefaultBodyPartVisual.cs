using System;
using Inventory.Item;
using UnityEngine;

namespace Inventory
{
    [Serializable]
    public class DefaultBodyPartVisual
    {
        [SerializeField] private BodyPart bodyPart;
        [SerializeField] private string visualName;

        public BodyPart BodyPart => bodyPart;
        public string VisualName => visualName;
    }
}
