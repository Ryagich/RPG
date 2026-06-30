using Inventory.Item;
using UnityEngine;

namespace NPC
{
    public sealed class NpcItemInterest : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private ItemHolder targetItem;
        [SerializeField, ReadOnlyInInspector] private string interestState = "None";
        [SerializeField, ReadOnlyInInspector] private float estimatedPickupTime;
        [SerializeField, ReadOnlyInInspector] private Vector3 homePosition;

        public ItemHolder TargetItem => targetItem;
        public string InterestState => interestState;
        public float EstimatedPickupTime => estimatedPickupTime;
        public Vector3 HomePosition => homePosition;
        public bool HasTarget => targetItem != null;

        public void SetTarget(ItemHolder item, Vector3 home, float eta)
        {
            targetItem = item;
            homePosition = home;
            interestState = item != null ? "Interested" : "None";
            estimatedPickupTime = Mathf.Max(0f, eta);
        }

        public void SetState(string state)
        {
            interestState = string.IsNullOrWhiteSpace(state) ? "None" : state;
        }

        public void UpdateEstimatedPickupTime(float eta)
        {
            estimatedPickupTime = Mathf.Max(0f, eta);
        }

        public void Clear()
        {
            targetItem = null;
            interestState = "None";
            estimatedPickupTime = 0f;
        }
    }
}
