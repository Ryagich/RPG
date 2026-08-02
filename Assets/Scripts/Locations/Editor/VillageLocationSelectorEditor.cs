using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Locations.Editor
{
    [CustomEditor(typeof(VillageLocationSelector))]
    public sealed class VillageLocationSelectorEditor : UnityEditor.Editor
    {
        private readonly Dictionary<string, bool> foldouts = new();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var locationsProperty = serializedObject.FindProperty("locations");
            DrawFirstSessionFields();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Locations", EditorStyles.boldLabel);

            for (var i = 0; i < locationsProperty.arraySize; i++)
            {
                DrawLocation(locationsProperty, i);
            }

            if (GUILayout.Button("Add location"))
            {
                locationsProperty.InsertArrayElementAtIndex(locationsProperty.arraySize);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFirstSessionFields()
        {
            var locationsProperty = serializedObject.FindProperty("locations");
            var ids = GetLocationIds(locationsProperty);
            DrawLocationPopup(
                new GUIContent("Default location"),
                serializedObject.FindProperty("defaultLocationId"),
                ids);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultPlayerTransform"));
        }

        private void DrawLocation(SerializedProperty locationsProperty, int index)
        {
            var locationProperty = locationsProperty.GetArrayElementAtIndex(index);
            var idProperty = locationProperty.FindPropertyRelative("id");
            var key = $"location-{index}";
            var title = string.IsNullOrWhiteSpace(idProperty.stringValue) ? $"Location {index + 1}" : idProperty.stringValue;
            foldouts.TryGetValue(key, out var expanded);
            expanded = EditorGUILayout.Foldout(expanded, title, true);
            foldouts[key] = expanded;
            if (!expanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(idProperty, new GUIContent("Location ID"));
            EditorGUILayout.PropertyField(locationProperty.FindPropertyRelative("requiredObjects"), new GUIContent("Required GameObjects"), true);
            DrawTransitions(locationsProperty, locationProperty, index);
            if (GUILayout.Button("Remove location"))
            {
                locationsProperty.DeleteArrayElementAtIndex(index);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawTransitions(SerializedProperty allLocationsProperty, SerializedProperty locationProperty, int locationIndex)
        {
            var transitionsProperty = locationProperty.FindPropertyRelative("transitions");
            EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);
            for (var i = 0; i < transitionsProperty.arraySize; i++)
            {
                var transitionProperty = transitionsProperty.GetArrayElementAtIndex(i);
                var key = $"transition-{locationIndex}-{i}";
                var id = transitionProperty.FindPropertyRelative("id").stringValue;
                foldouts.TryGetValue(key, out var expanded);
                expanded = EditorGUILayout.Foldout(expanded, string.IsNullOrWhiteSpace(id) ? $"Transition {i + 1}" : id, true);
                foldouts[key] = expanded;
                if (!expanded)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(transitionProperty.FindPropertyRelative("id"), new GUIContent("Transition ID"));
                EditorGUILayout.PropertyField(transitionProperty.FindPropertyRelative("playerSpawnTransform"), new GUIContent("Player spawn transform"));
                EditorGUILayout.PropertyField(transitionProperty.FindPropertyRelative("triggerZone"), new GUIContent("Trigger zone"));

                var targetLocationProperty = transitionProperty.FindPropertyRelative("targetLocationId");
                var targetTransitionProperty = transitionProperty.FindPropertyRelative("targetTransitionId");
                var canExit = transitionProperty.FindPropertyRelative("triggerZone").objectReferenceValue != null;
                if (!canExit)
                {
                    targetLocationProperty.stringValue = string.Empty;
                    targetTransitionProperty.stringValue = string.Empty;
                    EditorGUILayout.HelpBox("This transition has no Trigger Zone, so it is entrance-only and cannot lead to another location.", MessageType.Info);
                }
                else
                {
                    var targetLocationIds = GetLocationIds(allLocationsProperty);
                    DrawLocationPopup(new GUIContent("Target location"), targetLocationProperty, targetLocationIds);

                    if (!string.IsNullOrWhiteSpace(targetLocationProperty.stringValue))
                    {
                        var targetTransitions = GetTransitionIds(allLocationsProperty, targetLocationProperty.stringValue);
                        if (targetTransitions.Count == 0)
                        {
                            targetTransitionProperty.stringValue = string.Empty;
                            EditorGUILayout.HelpBox("This location has no transitions with a Player Spawn Transform, so it cannot be selected as a destination.", MessageType.Info);
                        }
                        else
                        {
                            DrawTransitionPopup(
                                new GUIContent("Target entrance"),
                                targetTransitionProperty,
                                targetTransitions);
                        }
                    }
                    else
                    {
                        targetTransitionProperty.stringValue = string.Empty;
                    }
                }

                if (GUILayout.Button("Remove transition"))
                {
                    transitionsProperty.DeleteArrayElementAtIndex(i);
                    EditorGUI.indentLevel--;
                    break;
                }

                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Add transition"))
            {
                transitionsProperty.InsertArrayElementAtIndex(transitionsProperty.arraySize);
            }
        }

        private static List<string> GetLocationIds(SerializedProperty locationsProperty)
        {
            var ids = new List<string>();
            for (var i = 0; i < locationsProperty.arraySize; i++)
            {
                var id = locationsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static List<string> GetTransitionIds(SerializedProperty locationsProperty, string locationId)
        {
            for (var i = 0; i < locationsProperty.arraySize; i++)
            {
                var locationProperty = locationsProperty.GetArrayElementAtIndex(i);
                if (locationProperty.FindPropertyRelative("id").stringValue != locationId)
                {
                    continue;
                }

                var result = new List<string>();
                var transitions = locationProperty.FindPropertyRelative("transitions");
                for (var transitionIndex = 0; transitionIndex < transitions.arraySize; transitionIndex++)
                {
                    var transition = transitions.GetArrayElementAtIndex(transitionIndex);
                    var id = transition.FindPropertyRelative("id").stringValue;
                    var hasPlayerSpawn = transition.FindPropertyRelative("playerSpawnTransform").objectReferenceValue != null;
                    if (hasPlayerSpawn && !string.IsNullOrWhiteSpace(id) && !result.Contains(id))
                    {
                        result.Add(id);
                    }
                }

                return result;
            }

            return new List<string>();
        }

        private static void DrawLocationPopup(GUIContent label, SerializedProperty property, List<string> ids)
        {
            DrawStringPopup(label, property, ids, "— Select location —");
        }

        private static void DrawTransitionPopup(GUIContent label, SerializedProperty property, List<string> ids)
        {
            DrawStringPopup(label, property, ids, "— Select transition —");
        }

        private static void DrawStringPopup(GUIContent label, SerializedProperty property, List<string> ids, string emptyLabel)
        {
            var options = new[] { emptyLabel }.Concat(ids).ToArray();
            var selectedIndex = string.IsNullOrWhiteSpace(property.stringValue) ? 0 : ids.IndexOf(property.stringValue) + 1;
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            var newIndex = EditorGUILayout.Popup(label, selectedIndex, options);
            property.stringValue = newIndex == 0 ? string.Empty : ids[newIndex - 1];
        }
    }
}
