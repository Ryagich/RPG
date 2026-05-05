using System;
using System.Collections.Generic;
using Inventory;
using Inventory.Item;
using UnityEditor;
using UnityEngine;
using BodyPart = Inventory.Item.BodyPart;

namespace EditorScripts
{
    [CustomPropertyDrawer(typeof(EquippedItemVisual))]
    public class EquippedItemVisualDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return BodyPartVisualSelectionDrawerUtility.GetPropertyHeight();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BodyPartVisualSelectionDrawerUtility.Draw(position, property);
        }
    }

    [CustomPropertyDrawer(typeof(DefaultBodyPartVisual))]
    public class DefaultBodyPartVisualDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return BodyPartVisualSelectionDrawerUtility.GetPropertyHeight();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BodyPartVisualSelectionDrawerUtility.Draw(position, property);
        }
    }

    internal static class BodyPartVisualSelectionDrawerUtility
    {
        public static float GetPropertyHeight()
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public static void Draw(Rect position, SerializedProperty property)
        {
            var bodyPartProperty = FindBodyPartProperty(property);
            var visualNameProperty = FindVisualNameProperty(property);
            if (bodyPartProperty == null || visualNameProperty == null)
            {
                EditorGUI.LabelField(position, "Unsupported property");
                return;
            }

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var bodyPartRect = new Rect(position.x, position.y, position.width, lineHeight);
            var visualNameRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            EditorGUI.PropertyField(bodyPartRect, bodyPartProperty);

            var bodyPart = GetBodyPartValue(bodyPartProperty);
            var currentVisualName = visualNameProperty.stringValue;
            var visualOptions = BodyPartVisualOptionsCache.GetVisualNames(bodyPart);
            var displayOptions = new List<string> { "<None>" };
            displayOptions.AddRange(visualOptions);

            var selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(currentVisualName))
            {
                selectedIndex = displayOptions.IndexOf(currentVisualName);
                if (selectedIndex < 0)
                {
                    displayOptions.Add($"[Missing] {currentVisualName}");
                    selectedIndex = displayOptions.Count - 1;
                }
            }

            using (new EditorGUI.DisabledScope(bodyPart == BodyPart.None))
            {
                var newIndex = EditorGUI.Popup(visualNameRect, "Visual Name", selectedIndex, displayOptions.ToArray());
                if (newIndex <= 0)
                {
                    visualNameProperty.stringValue = string.Empty;
                    return;
                }

                var selectedOption = displayOptions[newIndex];
                visualNameProperty.stringValue = selectedOption.StartsWith("[Missing] ", StringComparison.Ordinal)
                    ? currentVisualName
                    : selectedOption;
            }
        }

        private static BodyPart GetBodyPartValue(SerializedProperty bodyPartProperty)
        {
            return bodyPartProperty.propertyType == SerializedPropertyType.Enum
                ? (BodyPart)bodyPartProperty.intValue
                : BodyPart.None;
        }

        private static SerializedProperty FindBodyPartProperty(SerializedProperty property)
        {
            return property.FindPropertyRelative("bodyPart")
                   ?? property.FindPropertyRelative("<BodyPart>k__BackingField");
        }

        private static SerializedProperty FindVisualNameProperty(SerializedProperty property)
        {
            return property.FindPropertyRelative("visualName")
                   ?? property.FindPropertyRelative("<VisualName>k__BackingField");
        }
    }

    [InitializeOnLoad]
    internal static class BodyPartVisualOptionsCache
    {
        private static readonly Dictionary<BodyPart, string[]> Cache = new();
        private static bool isDirty = true;

        static BodyPartVisualOptionsCache()
        {
            EditorApplication.projectChanged += MarkDirty;
            EditorApplication.hierarchyChanged += MarkDirty;
        }

        public static string[] GetVisualNames(BodyPart bodyPart)
        {
            if (isDirty)
            {
                Rebuild();
            }

            return Cache.TryGetValue(bodyPart, out var options) ? options : Array.Empty<string>();
        }

        private static void MarkDirty()
        {
            isDirty = true;
        }

        private static void Rebuild()
        {
            isDirty = false;
            Cache.Clear();

            var namesByBodyPart = new Dictionary<BodyPart, SortedSet<string>>();
            foreach (BodyPart bodyPart in Enum.GetValues(typeof(BodyPart)))
            {
                if (bodyPart == BodyPart.None)
                {
                    continue;
                }

                namesByBodyPart[bodyPart] = new SortedSet<string>(StringComparer.Ordinal);
            }

            foreach (var visual in Resources.FindObjectsOfTypeAll<CharacterBodyPartVisual>())
            {
                AddVisual(visual, namesByBodyPart);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                foreach (var visual in prefab.GetComponentsInChildren<CharacterBodyPartVisual>(true))
                {
                    AddVisual(visual, namesByBodyPart);
                }
            }

            foreach (var pair in namesByBodyPart)
            {
                Cache[pair.Key] = new List<string>(pair.Value).ToArray();
            }
        }

        private static void AddVisual(CharacterBodyPartVisual visual, IReadOnlyDictionary<BodyPart, SortedSet<string>> namesByBodyPart)
        {
            if (visual == null || visual.BodyPart == BodyPart.None || string.IsNullOrWhiteSpace(visual.Name))
            {
                return;
            }

            if (!namesByBodyPart.TryGetValue(visual.BodyPart, out var visualNames))
            {
                return;
            }

            visualNames.Add(visual.Name);
        }
    }
}
