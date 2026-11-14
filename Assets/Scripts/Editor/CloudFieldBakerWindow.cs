using System;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public sealed class CloudFieldBakerWindow : EditorWindow
    {
        [SerializeField] private CloudBakeSettings settings;
        [SerializeField] private Vector2 scroll;
        private UnityEditor.Editor settingsEditor;
        private MaterialEditor previewEditor;
        private Material previewMaterial;
        private RenderTexture nearPreview;
        private RenderTexture distantPreview;
        private string status;

        [MenuItem("Tools/Rendering/Cloud Field Baker")]
        public static void Open()
        {
            var window = GetWindow<CloudFieldBakerWindow>("Cloud Field Baker");
            if (Selection.activeObject is CloudBakeSettings selected)
                window.SetSettings(selected);
            else if (Selection.activeObject is Material material)
            {
                window.SetSettings(null);
                foreach (string guid in AssetDatabase.FindAssets("t:CloudBakeSettings"))
                {
                    var candidate = AssetDatabase.LoadAssetAtPath<CloudBakeSettings>(AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate.material != material)
                        continue;
                    window.SetSettings(candidate);
                    break;
                }
            }
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearPreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ClearPreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ClearPreview();
            if (settingsEditor != null)
                DestroyImmediate(settingsEditor);
        }

        private void OnPlayModeChanged(PlayModeStateChange state) => ClearPreview();

        private void SetSettings(CloudBakeSettings value)
        {
            if (settings == value)
                return;
            ClearPreview();
            if (settingsEditor != null)
                DestroyImmediate(settingsEditor);
            settingsEditor = null;
            settings = value;
            status = null;
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var selected = (CloudBakeSettings)EditorGUILayout.ObjectField("Bake Settings", settings, typeof(CloudBakeSettings), false);
                if (selected != settings)
                    SetSettings(selected);
                if (GUILayout.Button("Create Bake Settings Asset"))
                    CreateSettings();
                if (settings == null)
                {
                    EditorGUILayout.HelpBox("Choose or create a recipe. Opening this window never generates textures.", MessageType.Info);
                    return;
                }
                scroll = EditorGUILayout.BeginScrollView(scroll);
                UnityEditor.Editor.CreateCachedEditor(settings, null, ref settingsEditor);
                EditorGUI.BeginChangeCheck();
                settingsEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    ClearPreview();
                    status = "Settings changed. Generate Preview or Bake explicitly to use the new values.";
                }

                EditorGUILayout.HelpBox("1. Edit the recipe. 2. Generate Preview to view it in this window. "
                    + "3. Bake and Assign to save a new texture pair and update the material. "
                    + "Settings changes never regenerate automatically. Preview never changes the scene or source material.", MessageType.Info);
                using (new EditorGUI.DisabledScope(!CloudFieldTextureGenerator.IsSupported))
                {
                    if (GUILayout.Button("Generate / Refresh Preview"))
                        RunCommand(GeneratePreview);
                    if (GUILayout.Button("Bake and Assign"))
                        RunCommand(() =>
                        {
                            CloudFieldBaker.Bake(settings);
                            ClearPreview();
                            status = "Bake saved and assigned. The material now uses the saved textures.";
                        });
                }
                if (!CloudFieldTextureGenerator.IsSupported)
                    EditorGUILayout.HelpBox("This graphics device cannot generate cloud fields. Rendering saved bakes does not require compute.", MessageType.Warning);
                if (!string.IsNullOrEmpty(status))
                    EditorGUILayout.HelpBox(status, MessageType.Info);
                if (previewEditor != null)
                {
                    EditorGUILayout.LabelField("Temporary Preview — not baked", EditorStyles.boldLabel);
                    Rect rect = GUILayoutUtility.GetRect(256f, 320f, GUILayout.ExpandWidth(true));
                    previewEditor.OnInteractivePreviewGUI(rect, GUIStyle.none);
                    if (GUILayout.Button("Discard Preview"))
                        ClearPreview();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void CreateSettings()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Cloud Bake Settings", "CloudBakeSettings", "asset", "Save the cloud recipe.");
            if (string.IsNullOrEmpty(path))
                return;
            var created = CreateInstance<CloudBakeSettings>();
            created.material = Selection.activeObject as Material;
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssetIfDirty(created);
            SetSettings(created);
        }

        private void RunCommand(Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception);
            }
        }

        private void GeneratePreview()
        {
            CloudFieldBaker.RequireSettings(settings);
            ClearPreview();
            try
            {
                var near = new CloudFieldSettings(settings.near);
                var distant = new CloudFieldSettings(settings.distant);
                nearPreview = CloudFieldTextureGenerator.Generate(near);
                distantPreview = CloudFieldTextureGenerator.Generate(distant);
                previewMaterial = new Material(settings.material) { hideFlags = HideFlags.HideAndDontSave };
                CloudFieldTextureGenerator.Assign(previewMaterial, nearPreview, near.Parameters, distantPreview, distant.Parameters);
                previewMaterial.SetFloat("_UseMainLight", 0f);
                previewEditor = (MaterialEditor)UnityEditor.Editor.CreateEditor(previewMaterial);
                status = "Preview only. Press Bake and Assign to save these settings as textures.";
            }
            catch
            {
                ClearPreview();
                throw;
            }
        }

        private void ClearPreview()
        {
            if (previewEditor != null)
                DestroyImmediate(previewEditor);
            if (previewMaterial != null)
                DestroyImmediate(previewMaterial);
            CloudFieldTextureGenerator.Release(nearPreview);
            CloudFieldTextureGenerator.Release(distantPreview);
            nearPreview = distantPreview = null;
            previewEditor = null;
            previewMaterial = null;
        }
    }
}
