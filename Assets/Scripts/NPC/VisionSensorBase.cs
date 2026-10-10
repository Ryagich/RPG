using Combat;
using UnityEngine;
using VContainer;

namespace NPC
{
    /// <summary>
    /// Owns the shared sector-trigger configuration used by vision candidate collectors.
    /// Concrete sensors decide which domain objects they collect.
    /// </summary>
    public abstract class VisionSensorBase : MonoBehaviour
    {
        private const string SectorTriggerName = "Vision Sector Trigger";
        private static readonly Vector3 SectorLocalOffset = Vector3.up;

        [SerializeField] protected NpcVisionConfig config;
        [SerializeField] protected SphereCollider sensorCollider;
        [SerializeField] protected Material previewMaterial;

        [Inject]
        public void Construct(NpcVisionConfig npcVisionConfig)
        {
            if (npcVisionConfig != null)
            {
                config = npcVisionConfig;
            }

            ConfigureSensorVolume();
        }

        protected virtual void Awake()
        {
            ConfigureSensorVolume();
        }

        protected virtual void OnValidate()
        {
            // Runtime creates the mesh collider without adding transient children to prefab assets.
        }

        internal void ReceiveTriggerEnter(Collider other) => OnVisionTriggerEnter(other);
        internal void ReceiveTriggerExit(Collider other) => OnVisionTriggerExit(other);

        protected abstract void OnVisionTriggerEnter(Collider other);
        protected abstract void OnVisionTriggerExit(Collider other);

        private void ConfigureSensorVolume()
        {
            sensorCollider = sensorCollider != null ? sensorCollider : GetComponent<SphereCollider>();
            if (sensorCollider != null)
            {
                // Legacy trigger from old prefabs. New and migrated prefabs have no such
                // component; disabling it also makes a partially migrated instance safe.
                sensorCollider.enabled = false;
            }

            var triggerTransform = transform.Find(SectorTriggerName);
            if (triggerTransform == null)
            {
                var triggerObject = new GameObject(SectorTriggerName);
                triggerTransform = triggerObject.transform;
                triggerTransform.SetParent(transform, false);
            }

            triggerTransform.localPosition = SectorLocalOffset;
            triggerTransform.localRotation = Quaternion.identity;

            var sectorTrigger = triggerTransform.GetComponent<VisionSectorTrigger>()
                                ?? triggerTransform.gameObject.AddComponent<VisionSectorTrigger>();
            sectorTrigger.Configure(this, config, previewMaterial);
            IgnoreOwnDamageHitboxes(sectorTrigger);
        }

        // Damage hitboxes describe the owner for incoming weapon strikes. They are not valid
        // perception candidates for that same owner, so leaving these trigger pairs enabled only
        // makes PhysX maintain redundant overlaps (especially on animated multi-hitbox rigs).
        private void IgnoreOwnDamageHitboxes(VisionSectorTrigger sectorTrigger)
        {
            var sectorCollider = sectorTrigger.GetComponent<Collider>();
            if (sectorCollider == null)
            {
                return;
            }

            // A perception sensor may live on a dedicated child object. Resolve the character
            // root first so every sensor owned by that character ignores the same hitboxes.
            var ownerRoot = transform.root;
            foreach (var hitbox in ownerRoot.GetComponentsInChildren<BodyHitbox>(true))
            {
                foreach (var hitboxCollider in hitbox.GetComponents<Collider>())
                {
                    if (hitboxCollider != sectorCollider)
                    {
                        Physics.IgnoreCollision(sectorCollider, hitboxCollider, true);
                    }
                }
            }
        }
    }
}
