using System;
using System.Linq;
using Combat;
using UnityEngine;

namespace Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerRagdollController : MonoBehaviour
    {
        private Rigidbody[] ragdollRigidbodies = Array.Empty<Rigidbody>();
        private Collider[] ragdollColliders = Array.Empty<Collider>();
        private bool isDeathRagdollActive;

        private void Awake()
        {
            CacheRagdollParts();
            ConfigureTriggerRagdoll();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                CacheRagdollParts();
            }
        }

        [ContextMenu("Setup Body Hitboxes")]
        public void SetupBodyHitboxes()
        {
            CacheRagdollParts();

            foreach (var ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider != null)
                {
                    EnsureBodyHitbox(ragdollCollider);
                }
            }
        }

        public void ConfigureTriggerRagdoll()
        {
            CacheRagdollParts();

            foreach (var ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider != null)
                {
                    ragdollCollider.isTrigger = true;
                    EnsureBodyHitbox(ragdollCollider);
                }
            }

            foreach (var rigidbody in ragdollRigidbodies)
            {
                if (rigidbody == null)
                {
                    continue;
                }

                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                rigidbody.detectCollisions = true;
            }
        }

        public void ActivateDeathRagdoll()
        {
            if (isDeathRagdollActive)
            {
                return;
            }

            isDeathRagdollActive = true;
            CacheRagdollParts();

            foreach (var ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider != null)
                {
                    ragdollCollider.isTrigger = false;
                    ragdollCollider.enabled = true;
                }
            }

            foreach (var rigidbody in ragdollRigidbodies)
            {
                if (rigidbody == null)
                {
                    continue;
                }

                rigidbody.isKinematic = false;
                rigidbody.useGravity = true;
                rigidbody.detectCollisions = true;
                rigidbody.WakeUp();
            }
        }

        public bool TryGetRagdollCenter(out Vector3 center)
        {
            CacheRagdollParts();

            var boundsInitialized = false;
            var bounds = default(Bounds);
            foreach (var ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider == null || !ragdollCollider.enabled)
                {
                    continue;
                }

                if (!boundsInitialized)
                {
                    bounds = ragdollCollider.bounds;
                    boundsInitialized = true;
                    continue;
                }

                bounds.Encapsulate(ragdollCollider.bounds);
            }

            if (boundsInitialized)
            {
                center = bounds.center;
                return true;
            }

            var count = 0;
            center = Vector3.zero;
            foreach (var rigidbody in ragdollRigidbodies)
            {
                if (rigidbody == null)
                {
                    continue;
                }

                center += rigidbody.worldCenterOfMass;
                count++;
            }

            if (count <= 0)
            {
                center = transform.position;
                return false;
            }

            center /= count;
            return true;
        }

        public Transform GetTopRagdollRootUnder(Transform ownerRoot)
        {
            CacheRagdollParts();
            if (ownerRoot == null || ragdollRigidbodies.Length == 0 || ragdollRigidbodies[0] == null)
            {
                return null;
            }

            var current = ragdollRigidbodies[0].transform;
            while (current.parent != null && current.parent != ownerRoot)
            {
                current = current.parent;
            }

            return current.parent == ownerRoot ? current : null;
        }

        private void CacheRagdollParts()
        {
            ragdollRigidbodies = GetComponentsInChildren<Rigidbody>(true)
                .Where(rigidbody => rigidbody != null && rigidbody.transform != transform)
                .ToArray();

            ragdollColliders = GetComponentsInChildren<Collider>(true)
                .Where(IsRagdollCollider)
                .ToArray();
        }

        private bool IsRagdollCollider(Collider collider)
        {
            if (collider == null
             || collider.transform == transform
             || collider.GetComponent<CharacterController>() != null)
            {
                return false;
            }

            var attachedRigidbody = collider.attachedRigidbody;
            return attachedRigidbody != null && ragdollRigidbodies.Contains(attachedRigidbody);
        }

        private static void EnsureBodyHitbox(Collider ragdollCollider)
        {
            var hitbox = ragdollCollider.GetComponent<BodyHitbox>();
            var wasCreated = hitbox == null;
            hitbox ??= ragdollCollider.gameObject.AddComponent<BodyHitbox>();
            hitbox.ConfigureDefaults(InferBodyPart(ragdollCollider.transform.name), wasCreated);
        }

        public static DamageBodyPart InferBodyPart(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return DamageBodyPart.None;
            }

            var name = objectName.ToLowerInvariant();
            if (name.Contains("head"))
            {
                return DamageBodyPart.Head;
            }

            if (name.Contains("hand"))
            {
                return DamageBodyPart.Hands;
            }

            if (name.Contains("foot") || name.Contains("toe"))
            {
                return DamageBodyPart.Feet;
            }

            if (name.Contains("arm") || name.Contains("shoulder"))
            {
                return DamageBodyPart.Arms;
            }

            if (name.Contains("leg") || name.Contains("thigh") || name.Contains("calf"))
            {
                return DamageBodyPart.Legs;
            }

            if (name.Contains("hip") || name.Contains("pelvis"))
            {
                return DamageBodyPart.Hips;
            }

            if (name.Contains("spine") || name.Contains("chest") || name.Contains("body") || name.Contains("torso"))
            {
                return DamageBodyPart.Body;
            }

            return DamageBodyPart.None;
        }
    }
}
