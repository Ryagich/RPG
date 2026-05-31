using System;
using UnityEngine;

namespace Inventory.Item
{
    [Serializable]
    public class WeaponAttachmentTransformData
    {
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;

        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
    }
}
