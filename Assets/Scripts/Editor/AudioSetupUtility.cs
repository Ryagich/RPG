using System.Collections.Generic;
using Container.Project;
using GameAudio;
using TMPro;
using UI.Configs;
using UI.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace EditorTools
{
    /// <summary>
    /// Creates only project-local audio assets. It is deliberately kept in Editor so the
    /// generated runtime assets have proper Unity references rather than copied GUID text.
    /// </summary>
    public static class AudioSetupUtility
    {
        private const string ConfigDirectory = "Assets/Configs/Audio";
        private const string SourcePrefabDirectory = "Assets/Content/Audio/Prefabs";
        private const string SettingsPrefabDirectory = "Assets/Prefabs/UI/Settings";
        private const string MixerPath = "Assets/Content/Audio/Mixers/RPG Audio Mixer.mixer";
        private const string UiHoverPath = "Assets/Content/Audio/UI/Button Hover.ogg";
        private const string UiClickPath = "Assets/Content/Audio/UI/Button Click.ogg";
        private const string UiConfigPath = "Assets/Configs/UI/UI Config.asset";

        [MenuItem("Tools/RPG/Audio/Create Or Repair Audio Setup")]
        public static void CreateOrRepairFromMenu()
        {
            Debug.Log(CreateOrRepair());
        }

        public static string CreateOrRepair()
        {
            EnsureFolder("Assets/Configs", "Audio");
            EnsureFolder("Assets/Content/Audio", "Prefabs");

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var hover = AssetDatabase.LoadAssetAtPath<AudioClip>(UiHoverPath);
            var click = AssetDatabase.LoadAssetAtPath<AudioClip>(UiClickPath);
            var footsteps = LoadFootsteps();
            if (mixer == null || hover == null || click == null || footsteps.Count == 0)
            {
                return "Audio setup was not created: one or more local audio assets are missing.";
            }

            var uiSource = GetOrCreateSource(
                SourcePrefabDirectory + "/UI Audio Source.prefab",
                "UI Audio Source",
                GetGroup(mixer, "UI"),
                false);
            var gameSource = GetOrCreateSource(
                SourcePrefabDirectory + "/Game Audio Source.prefab",
                "Game Audio Source",
                GetGroup(mixer, "Game"),
                true);
            var footstepsSource = GetOrCreateSource(
                SourcePrefabDirectory + "/Footsteps Audio Source.prefab",
                "Footsteps Audio Source",
                GetGroup(mixer, "Game"),
                true);

            var config = GetOrCreateConfig();
            ConfigureFootstepLayers(config, mixer, uiSource, gameSource, footstepsSource, hover, click, footsteps);

            var rowPrefab = GetOrCreateSoundSettingsRow();
            var pagePrefab = GetOrCreateSoundSettingsPage(rowPrefab);
            AssignSettingsPrefab(pagePrefab);
            InstallButtonAudio();
            AssignProjectConfig(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Audio setup created: config, 3 pooled-source prefabs, Sound Settings page and local references.";
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static List<AudioClip> LoadFootsteps()
        {
            var clips = new List<AudioClip>();
            for (var index = 1; index <= 5; index++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    $"Assets/Content/Audio/Footsteps/Default/S_CH_Footstep_{index:000}.wav");
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        private static AudioMixerGroup GetGroup(AudioMixer mixer, string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }

        private static AudioSource GetOrCreateSource(string path, string sourceName, AudioMixerGroup group, bool spatial)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioSource>(path);
            if (existing != null)
            {
                return existing;
            }

            var sourceObject = new GameObject(sourceName, typeof(AudioSource));
            var source = sourceObject.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = spatial ? 14f : 1f;
            source.outputAudioMixerGroup = group;
            PrefabUtility.SaveAsPrefabAsset(sourceObject, path);
            Object.DestroyImmediate(sourceObject);
            return AssetDatabase.LoadAssetAtPath<AudioSource>(path);
        }

        private static AudioConfig GetOrCreateConfig()
        {
            var path = ConfigDirectory + "/Audio Config.asset";
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(path);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<AudioConfig>();
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        private static void ConfigureFootstepLayers(
            AudioConfig config,
            AudioMixer mixer,
            AudioSource uiSource,
            AudioSource gameSource,
            AudioSource footstepsSource,
            AudioClip hover,
            AudioClip click,
            IReadOnlyList<AudioClip> footstepClips)
        {
            var defaultLayer = LayerMask.NameToLayer("Default");
            var grassLayer = LayerMask.NameToLayer("FootstepGrass");
            var dirtLayer = LayerMask.NameToLayer("FootstepDirt");
            var stoneLayer = LayerMask.NameToLayer("FootstepStone");
            var woodLayer = LayerMask.NameToLayer("FootstepWood");
            var metalLayer = LayerMask.NameToLayer("FootstepMetal");
            var mask = (1 << defaultLayer)
                       | (1 << grassLayer)
                       | (1 << dirtLayer)
                       | (1 << stoneLayer)
                       | (1 << woodLayer)
                       | (1 << metalLayer);
            var surfaces = new[]
            {
                new FootstepSurfaceSettings(1 << grassLayer, footstepClips),
                new FootstepSurfaceSettings(1 << dirtLayer, footstepClips),
                new FootstepSurfaceSettings(1 << stoneLayer, footstepClips),
                new FootstepSurfaceSettings(1 << woodLayer, footstepClips),
                new FootstepSurfaceSettings(1 << metalLayer, footstepClips),
            };

            config.ConfigureForProject(
                mixer,
                uiSource,
                gameSource,
                footstepsSource,
                hover,
                click,
                footstepClips,
                mask,
                surfaces);
            EditorUtility.SetDirty(config);
        }

        private static SoundSettingsRow GetOrCreateSoundSettingsRow()
        {
            var path = SettingsPrefabDirectory + "/Sound Settings Row.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<SoundSettingsRow>();
            if (existing != null)
            {
                return existing;
            }

            var rowObject = new GameObject(
                "Sound Settings Row",
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement),
                typeof(SoundSettingsRow));
            rowObject.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.15f, 0.94f);
            var rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(18, 18, 8, 8);
            rowLayout.spacing = 18f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowObject.GetComponent<LayoutElement>().minHeight = 66f;
            rowObject.GetComponent<LayoutElement>().preferredHeight = 66f;

            var title = CreateText("Title", rowObject.transform, "Громкость", TextAlignmentOptions.Left);
            var titleLayout = title.gameObject.GetComponent<LayoutElement>();
            titleLayout.minWidth = 200f;
            titleLayout.flexibleWidth = 1f;

            var slider = CreateSlider(rowObject.transform);

            var value = CreateText("Value", rowObject.transform, "100%", TextAlignmentOptions.Right);
            value.fontSize = 24f;
            var valueLayout = value.gameObject.GetComponent<LayoutElement>();
            valueLayout.preferredWidth = 70f;
            valueLayout.minWidth = 70f;

            var serializedRow = new SerializedObject(rowObject.GetComponent<SoundSettingsRow>());
            serializedRow.FindProperty("title").objectReferenceValue = title;
            serializedRow.FindProperty("value").objectReferenceValue = value;
            serializedRow.FindProperty("slider").objectReferenceValue = slider;
            serializedRow.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(rowObject, path);
            Object.DestroyImmediate(rowObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<SoundSettingsRow>();
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            var textComponent = textObject.GetComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.font = TMP_Settings.defaultFontAsset;
            textComponent.fontSize = 26f;
            textComponent.color = new Color(0.92f, 0.92f, 0.92f);
            textComponent.alignment = alignment;
            return textComponent;
        }

        private static Slider CreateSlider(Transform parent)
        {
            var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement));
            sliderObject.transform.SetParent(parent, false);
            sliderObject.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 0.9f);
            var sliderLayout = sliderObject.GetComponent<LayoutElement>();
            sliderLayout.preferredWidth = 330f;
            sliderLayout.minWidth = 180f;
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.wholeNumbers = false;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(9f, 9f), new Vector2(-9f, -9f));
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            fill.GetComponent<Image>().color = new Color(0.36f, 0.73f, 0.98f, 1f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), new Vector2(7f, 0f), new Vector2(-7f, 0f));
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(18f, 38f);
            var handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.96f, 0.97f, 1f, 1f);

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static void Stretch(RectTransform transform, Vector2 offsetMin, Vector2 offsetMax)
        {
            transform.anchorMin = Vector2.zero;
            transform.anchorMax = Vector2.one;
            transform.offsetMin = offsetMin;
            transform.offsetMax = offsetMax;
        }

        private static SoundSettingsPageUI GetOrCreateSoundSettingsPage(SoundSettingsRow rowPrefab)
        {
            var path = SettingsPrefabDirectory + "/Sound Settings.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<SoundSettingsPageUI>();
            if (existing != null)
            {
                var existingContents = PrefabUtility.LoadPrefabContents(path);
                var existingSerializedPage = new SerializedObject(existingContents.GetComponent<SoundSettingsPageUI>());
                existingSerializedPage.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
                existingSerializedPage.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(existingContents, path);
                PrefabUtility.UnloadPrefabContents(existingContents);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<SoundSettingsPageUI>();
            }

            var page = new GameObject("Sound Settings", typeof(RectTransform), typeof(SoundSettingsPageUI));
            var pageRect = page.GetComponent<RectTransform>();
            pageRect.anchorMin = new Vector2(0.30f, 0.10f);
            pageRect.anchorMax = new Vector2(0.92f, 0.82f);
            pageRect.offsetMin = Vector2.zero;
            pageRect.offsetMax = Vector2.zero;

            var scroll = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scroll.transform.SetParent(page.transform, false);
            Stretch(scroll.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            scroll.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.05f, 0.75f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scroll.transform, false);
            Stretch(viewport.GetComponent<RectTransform>(), new Vector2(14f, 14f), new Vector2(-14f, -14f));
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            var serializedPage = new SerializedObject(page.GetComponent<SoundSettingsPageUI>());
            serializedPage.FindProperty("rowsParent").objectReferenceValue = contentRect;
            serializedPage.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            serializedPage.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(page, path);
            Object.DestroyImmediate(page);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<SoundSettingsPageUI>();
        }

        private static void AssignSettingsPrefab(SoundSettingsPageUI pagePrefab)
        {
            var uiConfig = AssetDatabase.LoadAssetAtPath<UIConfig>(UiConfigPath);
            var serializedConfig = new SerializedObject(uiConfig);
            serializedConfig.FindProperty("<SoundSettings>k__BackingField").objectReferenceValue = pagePrefab;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(uiConfig);
        }

        private static void InstallButtonAudio()
        {
            AddButtonAudio("Assets/Prefabs/UI/Settings/Button_Settings.prefab");
            AddButtonAudio("Assets/Prefabs/UI/Settings/Title Sections.prefab");
            var uiConfig = AssetDatabase.LoadAssetAtPath<UIConfig>(UiConfigPath);
            AddButtonAudio(AssetDatabase.GetAssetPath(uiConfig.MenuUI));
        }

        private static void AddButtonAudio(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            var changed = false;
            foreach (var button in contents.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<UiButtonAudio>() == null)
                {
                    button.gameObject.AddComponent<UiButtonAudio>();
                    changed = true;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void AssignProjectConfig(AudioConfig config)
        {
            const string projectScopePrefabPath = "Assets/Resources/Project/ProjectLifetimeScope.prefab";
            var prefabContents = PrefabUtility.LoadPrefabContents(projectScopePrefabPath);
            var prefabScope = prefabContents.GetComponent<ProjectLifetimeScope>();
            if (prefabScope != null)
            {
                var prefabSerializedScope = new SerializedObject(prefabScope);
                prefabSerializedScope.FindProperty("<AudioConfig>k__BackingField").objectReferenceValue = config;
                prefabSerializedScope.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefabContents, projectScopePrefabPath);
            }

            PrefabUtility.UnloadPrefabContents(prefabContents);

            var scopes = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var scope in scopes)
            {
                var serializedScope = new SerializedObject(scope);
                serializedScope.FindProperty("<AudioConfig>k__BackingField").objectReferenceValue = config;
                serializedScope.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(scope);
                EditorSceneManager.MarkSceneDirty(scope.gameObject.scene);
                EditorSceneManager.SaveScene(scope.gameObject.scene);
            }
        }
    }
}
