using System.Collections.Generic;
using UnityEngine;

namespace NPC
{
    /// <summary>
    /// Runtime-only convex trigger volume for character perception. It owns the generated mesh
    /// and forwards physics events to the sensor collectors on its owner.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshCollider))]
    internal sealed class VisionSectorTrigger : MonoBehaviour
    {
        private const float LocalRadius = 0.5f;
        private const float MinimumHeight = .1f;
        private const float DegreesPerSegment = 5f;
        private const int MinimumSegments = 3;
        private const int MaximumSegments = 36;

        private readonly List<VisionSensorBase> receivers = new();
        private MeshCollider meshCollider;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh sectorMesh;
        private float configuredDistance = -1f;
        private float configuredAngle = -1f;
        private float configuredHeight = -1f;

        public void Configure(VisionSensorBase receiver, NpcVisionConfig config, Material previewMaterial)
        {
            if (receiver != null && !receivers.Contains(receiver))
            {
                receivers.Add(receiver);
            }

            var distance = config != null ? Mathf.Max(0.05f, config.ViewDistance) : LocalRadius;
            var angle = config != null ? Mathf.Clamp(config.ViewAngle, 1f, 360f) : 90f;
            var height = config != null ? Mathf.Max(MinimumHeight, config.ViewHeight) : MinimumHeight;
            EnsureCollider();

#if UNITY_EDITOR
            // This mesh is an authoring aid only. Perception itself uses the trigger collider;
            // creating a renderer in a player build would add an otherwise invisible draw call
            // for every active character sensor.
            if (!Application.isPlaying)
            {
                EnsurePreviewRenderer(previewMaterial);
            }
#endif

            if (!Mathf.Approximately(configuredDistance, distance)
             || !Mathf.Approximately(configuredAngle, angle)
             || !Mathf.Approximately(configuredHeight, height))
            {
                RebuildMesh(angle, height);
                configuredDistance = distance;
                configuredAngle = angle;
                configuredHeight = height;
            }

            // The mesh has a fixed local radius. Only horizontal scale represents sight range,
            // so its vertical extent remains a stable gameplay volume around the character.
            transform.localScale = new Vector3(distance / LocalRadius, 1f, distance / LocalRadius);
        }

        private void OnTriggerEnter(Collider other)
        {
            foreach (var receiver in receivers)
            {
                receiver?.ReceiveTriggerEnter(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            foreach (var receiver in receivers)
            {
                receiver?.ReceiveTriggerExit(other);
            }
        }

        private void OnDestroy()
        {
            if (sectorMesh != null)
            {
                Destroy(sectorMesh);
            }
        }

        private void EnsureCollider()
        {
            meshCollider ??= GetComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.isTrigger = true;
        }

        private void RebuildMesh(float angle, float height)
        {
            if (sectorMesh == null)
            {
                sectorMesh = new Mesh { name = "Vision Sector Trigger Mesh" };
            }

            var segments = Mathf.Clamp(Mathf.CeilToInt(angle / DegreesPerSegment), MinimumSegments, MaximumSegments);
            var halfHeight = height * 0.5f;
            var vertexCount = 2 + (segments + 1) * 2;
            var vertices = new Vector3[vertexCount];
            var bottomCenter = 0;
            var topCenter = 1;
            vertices[bottomCenter] = new Vector3(0f, -halfHeight, 0f);
            vertices[topCenter] = new Vector3(0f, halfHeight, 0f);

            var startAngle = -angle * 0.5f;
            for (var index = 0; index <= segments; index++)
            {
                var currentAngle = startAngle + angle * index / segments;
                var radians = currentAngle * Mathf.Deg2Rad;
                var point = new Vector3(Mathf.Sin(radians) * LocalRadius, 0f, Mathf.Cos(radians) * LocalRadius);
                var vertexOffset = 2 + index * 2;
                vertices[vertexOffset] = point + Vector3.down * halfHeight;
                vertices[vertexOffset + 1] = point + Vector3.up * halfHeight;
            }

            var triangles = new List<int>(segments * 12 + 12);
            for (var index = 0; index < segments; index++)
            {
                var currentBottom = 2 + index * 2;
                var currentTop = currentBottom + 1;
                var nextBottom = currentBottom + 2;
                var nextTop = currentBottom + 3;

                AddTriangle(triangles, bottomCenter, nextBottom, currentBottom);
                AddTriangle(triangles, topCenter, currentTop, nextTop);
                AddQuad(triangles, currentBottom, nextBottom, nextTop, currentTop);
            }

            var firstBottom = 2;
            var firstTop = firstBottom + 1;
            var lastBottom = 2 + segments * 2;
            var lastTop = lastBottom + 1;
            AddQuad(triangles, bottomCenter, firstBottom, firstTop, topCenter);
            AddQuad(triangles, lastBottom, bottomCenter, topCenter, lastTop);

            sectorMesh.Clear();
            sectorMesh.vertices = vertices;
            sectorMesh.triangles = triangles.ToArray();
            sectorMesh.RecalculateBounds();
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = sectorMesh;
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = sectorMesh;
            }
        }

        private void EnsurePreviewRenderer(Material previewMaterial)
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }

            meshRenderer.sharedMaterial = previewMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = previewMaterial != null;
        }

        private static void AddTriangle(List<int> triangles, int first, int second, int third)
        {
            triangles.Add(first);
            triangles.Add(second);
            triangles.Add(third);
        }

        private static void AddQuad(List<int> triangles, int first, int second, int third, int fourth)
        {
            AddTriangle(triangles, first, second, third);
            AddTriangle(triangles, first, third, fourth);
        }
    }
}
