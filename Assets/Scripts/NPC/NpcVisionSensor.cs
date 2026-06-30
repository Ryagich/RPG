using System.Collections.Generic;
using Inventory.Item;
using UnityEngine;
using VContainer;

namespace NPC
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class NpcVisionSensor : MonoBehaviour
    {
        private readonly HashSet<ItemHolder> itemCandidates = new();
        private readonly HashSet<NpcItemInterest> npcCandidates = new();

        [SerializeField] private NpcVisionConfig config;
        [SerializeField] private SphereCollider sensorCollider;

        public IEnumerable<ItemHolder> ItemCandidates => itemCandidates;
        public IEnumerable<NpcItemInterest> NpcCandidates => npcCandidates;
        public bool HasItemCandidates => itemCandidates.Count > 0;

        [Inject]
        public void Construct(NpcVisionConfig npcVisionConfig)
        {
            if (npcVisionConfig != null)
            {
                config = npcVisionConfig;
            }

            ConfigureCollider();
        }

        private void Awake()
        {
            ConfigureCollider();
        }

        private void OnValidate()
        {
            ConfigureCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            var itemHolder = other.GetComponentInParent<ItemHolder>();
            if (itemHolder != null && itemHolder.CanInteractable)
            {
                itemCandidates.Add(itemHolder);
            }

            var npcInterest = other.GetComponentInParent<NpcItemInterest>();
            if (npcInterest != null && npcInterest.gameObject != gameObject)
            {
                npcCandidates.Add(npcInterest);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null)
            {
                return;
            }

            var itemHolder = other.GetComponentInParent<ItemHolder>();
            if (itemHolder != null)
            {
                itemCandidates.Remove(itemHolder);
            }

            var npcInterest = other.GetComponentInParent<NpcItemInterest>();
            if (npcInterest != null)
            {
                npcCandidates.Remove(npcInterest);
            }
        }

        public void PruneInvalidCandidates()
        {
            itemCandidates.RemoveWhere(item => item == null || !item.CanInteractable);
            npcCandidates.RemoveWhere(npc => npc == null || npc.gameObject == gameObject);
        }

        private void ConfigureCollider()
        {
            sensorCollider = sensorCollider != null ? sensorCollider : GetComponent<SphereCollider>();
            if (sensorCollider == null)
            {
                return;
            }

            sensorCollider.isTrigger = true;
            sensorCollider.radius = config != null ? Mathf.Max(0.05f, config.ViewDistance) : 0.05f;
            sensorCollider.center = Vector3.zero;
        }
    }
}
