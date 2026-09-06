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
            var gender = GetCharacterGender(property);
            var visualOptions = BodyPartVisualOptionsCache.GetVisualNames(bodyPart, gender);
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

            var isUnavailableForGender = bodyPart == BodyPart.Beard && gender == CharacterGender.Female;
            if (isUnavailableForGender)
            {
                visualNameProperty.stringValue = string.Empty;
            }

            using (new EditorGUI.DisabledScope(bodyPart == BodyPart.None || isUnavailableForGender))
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

        private static CharacterGender GetCharacterGender(SerializedProperty property)
        {
            if (property.serializedObject.targetObject is CharacterVisualConfig)
            {
                var genderProperty = property.serializedObject.FindProperty("<Gender>k__BackingField");
                if (genderProperty != null)
                {
                    return (CharacterGender)genderProperty.enumValueIndex;
                }
            }

            return property.propertyPath.StartsWith("femaleEquippedVisuals", StringComparison.Ordinal)
                ? CharacterGender.Female
                : CharacterGender.Male;
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
        private static readonly Dictionary<(BodyPart BodyPart, CharacterGender Gender), string[]> Cache = new();
        private static bool isDirty = true;

        static BodyPartVisualOptionsCache()
        {
            EditorApplication.projectChanged += MarkDirty;
        }

        public static string[] GetVisualNames(BodyPart bodyPart, CharacterGender gender)
        {
            if (isDirty)
            {
                RebuildFromLoadedVisuals();
            }

            return Cache.TryGetValue((bodyPart, gender), out var options) ? options : Array.Empty<string>();
        }

        private static void MarkDirty()
        {
            isDirty = true;
        }

        private static void RebuildFromLoadedVisuals()
        {
            isDirty = false;
            Cache.Clear();

            var namesByBodyPart = new Dictionary<(BodyPart BodyPart, CharacterGender Gender), SortedSet<string>>();
            foreach (BodyPart bodyPart in Enum.GetValues(typeof(BodyPart)))
            {
                if (bodyPart == BodyPart.None)
                {
                    continue;
                }

                foreach (CharacterGender gender in Enum.GetValues(typeof(CharacterGender)))
                {
                    namesByBodyPart[(bodyPart, gender)] = new SortedSet<string>(StringComparer.Ordinal);
                }
            }

            foreach (var visual in Resources.FindObjectsOfTypeAll<CharacterBodyPartVisual>())
            {
                AddVisual(visual, namesByBodyPart);
            }

            foreach (var pair in namesByBodyPart)
            {
                Cache[pair.Key] = new List<string>(pair.Value).ToArray();
            }
        }

        private static void AddVisual(
            CharacterBodyPartVisual visual,
            IReadOnlyDictionary<(BodyPart BodyPart, CharacterGender Gender), SortedSet<string>> namesByBodyPart)
        {
            if (visual == null || visual.BodyPart == BodyPart.None || string.IsNullOrWhiteSpace(visual.Name))
            {
                return;
            }

            foreach (CharacterGender gender in Enum.GetValues(typeof(CharacterGender)))
            {
                if (visual.IsAvailableFor(gender)
                    && namesByBodyPart.TryGetValue((visual.BodyPart, gender), out var visualNames))
                {
                    visualNames.Add(visual.Name);
                }
            }
        }
    }
}
