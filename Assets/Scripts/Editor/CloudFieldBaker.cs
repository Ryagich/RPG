using System;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    internal static class CloudFieldBaker
    {
        public static void RequireSettings(CloudBakeSettings settings)
        {
            CloudFieldTextureGenerator.RequireEditMode();
            if (settings == null || settings.near == null || settings.distant == null)
                throw new ArgumentException("Choose a cloud bake settings asset with both layers.");
            if (settings.material == null || settings.material.shader == null
                || settings.material.shader.name != CloudFieldTextureGenerator.SkyShaderName
                || !AssetDatabase.Contains(settings.material))
                throw new ArgumentException("Choose a Low Poly Clouds material asset.");
        }

        public static void Bake(CloudBakeSettings settings)
        {
            RequireSettings(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            string folder = settings.outputFolder?.TrimEnd('/', '\\');
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)
                || (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal)))
                throw new ArgumentException("The output folder must be an existing folder inside Assets.");

            var nearSettings = new CloudFieldSettings(settings.near);
            var distantSettings = new CloudFieldSettings(settings.distant);
            Texture2D near = null;
            Texture2D distant = null;
            string nearPath = null;
            string distantPath = null;
            bool committed = false;
            bool assigned = false;
            var previous = new Material(settings.material) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                EditorUtility.DisplayProgressBar("Cloud Field Baker", "Generating near layer", 0.1f);
                near = ReadField(nearSettings);
                EditorUtility.DisplayProgressBar("Cloud Field Baker", "Generating distant layer", 0.5f);
                distant = ReadField(distantSettings);
                string safeName = string.Concat(settings.name.Split(System.IO.Path.GetInvalidFileNameChars()));
                near.name = safeName + " Near Cloud Field";
                distant.name = safeName + " Distant Cloud Field";
                nearPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeName + "_Near.asset");
                AssetDatabase.CreateAsset(near, nearPath);
                distantPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeName + "_Distant.asset");
                AssetDatabase.CreateAsset(distant, distantPath);
                AssetDatabase.SaveAssetIfDirty(near);
                AssetDatabase.SaveAssetIfDirty(distant);
                Undo.RecordObject(settings.material, "Assign baked cloud fields");
                assigned = true;
                CloudFieldTextureGenerator.Assign(settings.material, near, nearSettings.Parameters,
                    distant, distantSettings.Parameters);
                EditorUtility.SetDirty(settings.material);
                AssetDatabase.SaveAssetIfDirty(settings.material);
                committed = true;
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
                Debug.Log("Cloud bake saved and assigned: " + nearPath + ", " + distantPath, settings.material);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!committed)
                {
                    if (assigned)
                    {
                        settings.material.CopyPropertiesFromMaterial(previous);
                        EditorUtility.SetDirty(settings.material);
                        AssetDatabase.SaveAssetIfDirty(settings.material);
                    }
                    RemoveUncommitted(near, nearPath);
                    RemoveUncommitted(distant, distantPath);
                }
                UnityEngine.Object.DestroyImmediate(previous);
            }
        }

        private static void RemoveUncommitted(Texture2D texture, string path)
        {
            if (texture == null)
                return;
            if (path != null && AssetDatabase.Contains(texture))
                AssetDatabase.DeleteAsset(path);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }

        private static Texture2D ReadField(CloudFieldSettings settings)
        {
            RenderTexture field = CloudFieldTextureGenerator.Generate(settings);
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(settings.Resolution, settings.Resolution, TextureFormat.RGBAHalf, true, true)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4
                };
                RenderTexture.active = field;
                texture.ReadPixels(new Rect(0, 0, field.width, field.height), 0, 0, false);
                texture.Apply(true, true);
                return texture;
            }
            catch
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                CloudFieldTextureGenerator.Release(field);
            }
        }
    }
}
