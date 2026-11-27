using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace World.Grass
{
    /// <summary>Scene-owned visual simulation. It observes characters after movement;
    /// neither movement nor combat depends on this system.</summary>
    public sealed class GrassDeformationField : IStartable, ILateTickable, IDisposable
    {
        private static readonly int FieldRectId = Shader.PropertyToID("_RpgGrassFieldRect");
        private static readonly int FieldId = Shader.PropertyToID("_RpgGrassField");
        private static readonly int BendId = Shader.PropertyToID("_RpgGrassBend");
        private static GrassDeformationField publishedOwner;
        private readonly GrassDeformationConfig config;
        private readonly GrassDeformationRegistry registry;
        private readonly GrassDeformationSample[] selected;
        private readonly float[] scores;
        private readonly Vector4[] previousData = new Vector4[64];
        private readonly Vector4[] currentData = new Vector4[64];
        private readonly HashSet<GrassDeformationSource> previousSelection = new();
        private readonly List<(Renderer renderer, Bounds bounds)> adjustedBounds = new();
        private readonly List<Material> materials = new();
        private RenderTexture previousField;
        private RenderTexture nextField;
        private ComputeShader compute;
        private int kernel;
        private int resolution;
        private int selectedCount;
        private float worldSize;
        private Vector2Int originCell;
        private GrassDeformationSource previousAnchor;
        private bool hasHistory;
        private bool ready;
        private bool initializationFailed;
        private Vector3 lastAnchorPosition;

        public int ActiveSourceCount => selectedCount;
        public RenderTexture CurrentField => previousField;

        public GrassDeformationField(GrassDeformationConfig config, GrassDeformationRegistry registry)
        {
            this.config = config;
            this.registry = registry;
            selected = new GrassDeformationSample[64];
            scores = new float[64];
        }

        public void Start()
        {
            if (config.Enabled) Initialize();
        }

        private void Initialize()
        {
            var format = GraphicsFormat.R16G16B16A16_SFloat;
            if (config.Compute == null || config.GrassShader == null
                || !SystemInfo.supportsComputeShaders
                || !SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore)
                || !SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample)
                || !SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Linear))
            {
                initializationFailed = true;
                Debug.LogWarning("Grass deformation is unavailable: compute, shader, or filterable RGBAHalf load/store support is missing. Grass keeps its normal wind animation.");
                return;
            }

            resolution = Mathf.Clamp(config.Resolution, 32, SystemInfo.maxTextureSize);
            worldSize = Mathf.Max(4f, config.WorldSize);
            try
            {
                compute = Object.Instantiate(config.Compute);
                kernel = compute.FindKernel("UpdateField");
                previousField = CreateTexture("Grass deformation history", format);
                nextField = CreateTexture("Grass deformation output", format);
                ExpandRenderBounds(default, allLoadedScenes: true);
                SceneManager.sceneLoaded += OnSceneLoaded;
                ready = true;
                initializationFailed = false;
            }
            catch (Exception exception)
            {
                Dispose();
                initializationFailed = true;
                Debug.LogException(exception);
            }
        }

        private RenderTexture CreateTexture(string name, GraphicsFormat format)
        {
            var texture = new RenderTexture(resolution, resolution, 0)
            {
                name = name,
                graphicsFormat = format,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (texture.Create()) return texture;
            Object.Destroy(texture);
            throw new InvalidOperationException("Cannot allocate grass deformation texture.");
        }

        public void LateTick()
        {
            if (!config.Enabled)
            {
                if (ready || initializationFailed) Dispose();
                return;
            }
            // Rebuild only when allocation settings change, including edits in Play mode.
            if (ready && (resolution != Mathf.Clamp(config.Resolution, 32, SystemInfo.maxTextureSize)
                          || !Mathf.Approximately(worldSize, Mathf.Max(4f, config.WorldSize))))
                Dispose();
            if (!ready)
            {
                if (initializationFailed) return;
                Initialize();
                if (!ready) return;
            }
            var anchor = registry.Anchor;
            if (anchor == null || !anchor.IsActive)
            {
                if (hasHistory) ResetSourceHistory();
                hasHistory = false;
                selectedCount = 0;
                previousSelection.Clear();
                ClearGlobals();
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            var position = anchor.AnchorPosition;
            float texelSize = worldSize / resolution;
            var nextOrigin = new Vector2Int(
                Mathf.FloorToInt((position.x - worldSize * 0.5f) / texelSize),
                Mathf.FloorToInt((position.z - worldSize * 0.5f) / texelSize));
            var offset = nextOrigin - originCell;
            bool clear = !hasHistory || previousAnchor != anchor
                || Mathf.Abs(offset.x) >= resolution || Mathf.Abs(offset.y) >= resolution
                || (hasHistory && (position - lastAnchorPosition).sqrMagnitude
                    > config.MaxSweepDistance * config.MaxSweepDistance);
            if (clear) previousSelection.Clear();
            originCell = nextOrigin;
            previousAnchor = anchor;
            lastAnchorPosition = position;

            SelectSources(anchor, position, texelSize);
            for (int i = 0; i < selectedCount; i++)
            {
                var sample = selected[i];
                previousData[i] = new Vector4(sample.Previous.x, sample.Previous.y, sample.Previous.z, sample.Radius);
                currentData[i] = new Vector4(sample.Current.x, sample.Current.y, sample.Current.z, sample.Strength);
            }

            compute.SetInt("_Resolution", resolution);
            compute.SetInts("_HistoryOffset", offset.x, offset.y);
            compute.SetInt("_ClearHistory", clear ? 1 : 0);
            compute.SetVector("_FieldOrigin", new Vector4(originCell.x * texelSize, originCell.y * texelSize, texelSize, 0));
            compute.SetFloat("_Decay", Mathf.Exp(-Mathf.Log(2f) * deltaTime / Mathf.Max(0.05f, config.RecoveryHalfLife)));
            compute.SetFloat("_PressResponse", 1f - Mathf.Exp(-Mathf.Max(0.05f, config.PressSpeed) * deltaTime));
            compute.SetFloat("_HeightTolerance", Mathf.Max(0.05f, config.HeightTolerance));
            compute.SetInt("_SourceCount", selectedCount);
            compute.SetVectorArray("_SourcePrevious", previousData);
            compute.SetVectorArray("_SourceCurrent", currentData);
            compute.SetTexture(kernel, "_PreviousField", previousField);
            compute.SetTexture(kernel, "_NextField", nextField);
            // Ordered on the graphics queue, before all cameras and shadow rendering.
            compute.Dispatch(kernel, (resolution + 7) / 8, (resolution + 7) / 8, 1);
            (previousField, nextField) = (nextField, previousField);
            hasHistory = true;
            publishedOwner = this;
            Shader.SetGlobalTexture(FieldId, previousField);
            Shader.SetGlobalVector(FieldRectId, new Vector4(originCell.x * texelSize,
                originCell.y * texelSize, 1f / worldSize, 1f));
            Shader.SetGlobalVector(BendId, new Vector4(config.HorizontalBend, config.DownwardBend,
                config.HeightTolerance, config.MaxBladeHeight));
        }

        private void SelectSources(GrassDeformationSource anchor, Vector3 anchorPosition, float texelSize)
        {
            selectedCount = 0;
            // Slot zero remains reserved even while the player is airborne.
            bool anchorSampled = anchor.TrySample(config, out var anchorSample);
            if (anchorSampled) selected[selectedCount++] = anchorSample;
            int firstNpc = selectedCount;
            int capacity = Mathf.Clamp(config.EntitySlots, 1, selected.Length) - (anchorSampled ? 0 : 1);
            var minimum = new Vector2(originCell.x * texelSize, originCell.y * texelSize);
            var maximum = minimum + Vector2.one * worldSize;

            foreach (var source in registry.Sources)
            {
                if (source == null || source == anchor
                    || !source.TrySample(config, out var sample)) continue;
                if (Mathf.Max(sample.Previous.x, sample.Current.x) + sample.Radius < minimum.x
                    || Mathf.Min(sample.Previous.x, sample.Current.x) - sample.Radius > maximum.x
                    || Mathf.Max(sample.Previous.z, sample.Current.z) + sample.Radius < minimum.y
                    || Mathf.Min(sample.Previous.z, sample.Current.z) - sample.Radius > maximum.y) continue;

                var delta = sample.Current - anchorPosition;
                float score = new Vector2(delta.x, delta.z).magnitude
                    - (previousSelection.Contains(source) ? config.SelectionHysteresis : 0f);
                int insert = firstNpc;
                while (insert < selectedCount && scores[insert] <= score) insert++;
                if (insert >= capacity) continue;
                int end = Mathf.Min(selectedCount, capacity - 1);
                for (int i = end; i > insert; i--)
                {
                    selected[i] = selected[i - 1];
                    scores[i] = scores[i - 1];
                }
                selected[insert] = sample;
                scores[insert] = score;
                selectedCount = Mathf.Min(selectedCount + 1, capacity);
            }
            previousSelection.Clear();
            for (int i = firstNpc; i < selectedCount; i++) previousSelection.Add(selected[i].Source);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ExpandRenderBounds(scene, false);

        private void ExpandRenderBounds(Scene scene, bool allLoadedScenes)
        {
            // One scan at startup / scene load, never per frame. Shader displacement can
            // extend beyond authored mesh bounds; preserve original overrides for cleanup.
            var margin = new Vector3(config.HorizontalBend, config.DownwardBend, config.HorizontalBend);
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!allLoadedScenes && renderer.gameObject.scene != scene) continue;
                renderer.GetSharedMaterials(materials);
                bool affected = false;
                foreach (var material in materials)
                    if (material != null && material.shader == config.GrassShader) { affected = true; break; }
                if (!affected) continue;
                var bounds = renderer.localBounds;
                adjustedBounds.Add((renderer, bounds));
                var inverse = renderer.worldToLocalMatrix;
                bounds.extents += new Vector3(
                    Vector3.Dot(new Vector3(Mathf.Abs(inverse.m00), Mathf.Abs(inverse.m01), Mathf.Abs(inverse.m02)), margin),
                    Vector3.Dot(new Vector3(Mathf.Abs(inverse.m10), Mathf.Abs(inverse.m11), Mathf.Abs(inverse.m12)), margin),
                    Vector3.Dot(new Vector3(Mathf.Abs(inverse.m20), Mathf.Abs(inverse.m21), Mathf.Abs(inverse.m22)), margin));
                renderer.localBounds = bounds;
            }
        }

        private void ClearGlobals()
        {
            if (publishedOwner != this) return;
            Shader.SetGlobalVector(FieldRectId, Vector4.zero);
            Shader.SetGlobalTexture(FieldId, null);
            publishedOwner = null;
        }

        private void ResetSourceHistory()
        {
            foreach (var source in registry.Sources)
                source?.ResetHistory();
        }

        public void Dispose()
        {
            ready = hasHistory = false;
            initializationFailed = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ClearGlobals();
            foreach (var entry in adjustedBounds)
                if (entry.renderer != null) entry.renderer.localBounds = entry.bounds;
            adjustedBounds.Clear();
            previousSelection.Clear();
            ResetSourceHistory();
            Array.Clear(selected, 0, selected.Length);
            previousAnchor = null;
            selectedCount = 0;
            Release(previousField);
            Release(nextField);
            if (compute != null) Object.Destroy(compute);
            previousField = nextField = null;
            compute = null;
        }

        private static void Release(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Object.Destroy(texture);
        }
    }
}
