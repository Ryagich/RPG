using System;
using System.Collections.Generic;
using Factions;
using Inventory.Item;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    internal abstract class ConfigIconPreviewEditor<TConfig> : UnityEditor.Editor
        where TConfig : ScriptableObject
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }

        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
        {
            var config = target as TConfig;
            return config == null ? null : ConfigIconPreviewCache.GetPreview(GetIcon(config), width, height);
        }

        protected abstract Sprite GetIcon(TConfig config);
    }

    [CustomEditor(typeof(ItemConfig), true)]
    [CanEditMultipleObjects]
    internal sealed class ItemConfigPreviewEditor : ConfigIconPreviewEditor<ItemConfig>
    {
        protected override Sprite GetIcon(ItemConfig config) => config.Icon;
    }

    [CustomEditor(typeof(FactionConfig))]
    [CanEditMultipleObjects]
    internal sealed class FactionConfigPreviewEditor : ConfigIconPreviewEditor<FactionConfig>
    {
        protected override Sprite GetIcon(FactionConfig config) => config.Icon;
    }

    [InitializeOnLoad]
    internal static class ConfigIconPreviewCache
    {
        private static readonly Dictionary<PreviewKey, Texture2D> previews = new();

        static ConfigIconPreviewCache()
        {
            EditorApplication.projectChanged += Clear;
            Undo.undoRedoPerformed += Clear;
        }

        public static Texture2D GetPreview(Sprite icon, int width, int height)
        {
            if (icon == null || icon.texture == null || width <= 0 || height <= 0)
            {
                return null;
            }

            var key = new PreviewKey(icon.GetInstanceID(), width, height);
            if (previews.TryGetValue(key, out var preview) && preview != null)
            {
                return preview;
            }

            preview = CreatePreview(icon, width, height);
            if (preview != null)
            {
                previews[key] = preview;
            }

            return preview;
        }

        private static Texture2D CreatePreview(Sprite icon, int width, int height)
        {
            var texture = icon.texture;
            var textureRect = icon.textureRect;
            if (texture == null || textureRect.width <= 0f || textureRect.height <= 0f)
            {
                return null;
            }

            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previousRenderTexture = RenderTexture.active;
            try
            {
                var scale = new Vector2(textureRect.width / texture.width, textureRect.height / texture.height);
                var offset = new Vector2(textureRect.x / texture.width, textureRect.y / texture.height);
                Graphics.Blit(texture, renderTexture, scale, offset);

                RenderTexture.active = renderTexture;
                var preview = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = $"{icon.name} Preview",
                    hideFlags = HideFlags.HideAndDontSave
                };
                preview.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                preview.Apply(false, true);
                return preview;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static void Clear()
        {
            foreach (var preview in previews.Values)
            {
                if (preview != null)
                {
                    UnityEngine.Object.DestroyImmediate(preview);
                }
            }

            previews.Clear();
            EditorApplication.RepaintProjectWindow();
        }

        private readonly struct PreviewKey : IEquatable<PreviewKey>
        {
            private readonly int iconId;
            private readonly int width;
            private readonly int height;

            public PreviewKey(int iconId, int width, int height)
            {
                this.iconId = iconId;
                this.width = width;
                this.height = height;
            }

            public bool Equals(PreviewKey other) =>
                iconId == other.iconId && width == other.width && height == other.height;

            public override bool Equals(object obj) => obj is PreviewKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(iconId, width, height);
        }
    }

    [InitializeOnLoad]
    internal static class ConfigIconPreviewPrewarmer
    {
        private const int PreviewCacheSize = 256;
        private const int PreviewsPerEditorUpdate = 8;

        private static readonly Queue<UnityEngine.Object> pendingAssets = new();
        private static bool isPrewarming;

        static ConfigIconPreviewPrewarmer()
        {
            EditorApplication.delayCall += StartPrewarming;
            EditorApplication.projectChanged += SchedulePrewarming;
        }

        private static void SchedulePrewarming()
        {
            if (isPrewarming)
            {
                return;
            }

            EditorApplication.delayCall -= StartPrewarming;
            EditorApplication.delayCall += StartPrewarming;
        }

        private static void StartPrewarming()
        {
            if (isPrewarming)
            {
                return;
            }

            isPrewarming = true;
            pendingAssets.Clear();
            AssetPreview.SetPreviewTextureCacheSize(PreviewCacheSize);
            EnqueueAssets<ItemConfig>(config => config.Icon);
            EnqueueAssets<FactionConfig>(config => config.Icon);

            EditorApplication.update -= PrewarmNextPreviews;
            EditorApplication.update += PrewarmNextPreviews;
        }

        private static void EnqueueAssets<TConfig>(Func<TConfig, Sprite> getIcon)
            where TConfig : ScriptableObject
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(TConfig).Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<TConfig>(path);
                if (config != null && getIcon(config) != null)
                {
                    pendingAssets.Enqueue(config);
                }
            }
        }

        private static void PrewarmNextPreviews()
        {
            for (var i = 0; i < PreviewsPerEditorUpdate && pendingAssets.Count > 0; i++)
            {
                var asset = pendingAssets.Dequeue();
                var preview = AssetPreview.GetAssetPreview(asset);
                if (preview == null && AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID()))
                {
                    pendingAssets.Enqueue(asset);
                }
            }

            if (pendingAssets.Count > 0)
            {
                return;
            }

            EditorApplication.update -= PrewarmNextPreviews;
            isPrewarming = false;
            EditorApplication.RepaintProjectWindow();
        }
    }
}
