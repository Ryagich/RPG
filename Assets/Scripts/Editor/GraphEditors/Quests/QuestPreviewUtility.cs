using System.Collections.Generic;
using Quests.Graph;
using Quests.Graph.Model;
using EditorTools;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Quests.Editor
{
    public static class QuestPreviewUtility
    {
        private const string PreferredPreviewLocale = "ru";
        private static readonly Dictionary<QuestNodeData, SerializedObject> serializedNodeObjects = new();
        private static readonly Dictionary<QuestGraph, SerializedObject> serializedGraphObjects = new();

        static QuestPreviewUtility()
        {
            EditorApplication.projectChanged += InvalidateCaches;
        }

        public static string GetNodeEditorTitle(QuestNodeData nodeData)
        {
            return nodeData == null
                ? "Quest Node"
                : nodeData.EditorTitle;
        }

        public static string GetNodeDisplayName(QuestNodeData nodeData)
        {
            if (nodeData == null)
            {
                return "Quest Node";
            }

            SerializedObject nodeObject = GetSerializedNodeObject(nodeData);
            SerializedProperty localizedNameProperty = nodeObject.FindProperty("localizedName");
            return GetLocalizedStringDisplayName(localizedNameProperty, nodeData.name);
        }

        public static string GetNodeDescription(QuestNodeData nodeData)
        {
            if (nodeData == null)
            {
                return string.Empty;
            }

            SerializedObject nodeObject = GetSerializedNodeObject(nodeData);
            SerializedProperty localizedDescriptionProperty = nodeObject.FindProperty("localizedDescription");
            return GetLocalizedStringPreviewValue(localizedDescriptionProperty);
        }

        public static string GetQuestDisplayName(QuestGraph questGraph)
        {
            if (questGraph == null)
            {
                return "Quest";
            }

            SerializedObject graphObject = GetSerializedGraphObject(questGraph);
            SerializedProperty titleProperty = graphObject.FindProperty("title");
            return GetLocalizedStringDisplayName(titleProperty, questGraph.name);
        }

        public static string GetQuestDescription(QuestGraph questGraph)
        {
            if (questGraph == null)
            {
                return string.Empty;
            }

            SerializedObject graphObject = GetSerializedGraphObject(questGraph);
            SerializedProperty descriptionProperty = graphObject.FindProperty("description");
            return GetLocalizedStringDisplayName(descriptionProperty, questGraph.name);
        }

        public static void DrawQuestGraphPreview(QuestGraph questGraph, string header = "Quest Preview")
        {
            if (questGraph == null)
            {
                return;
            }

            QuestNodeData entryNode = questGraph.GetEntryNode();
            Sprite sprite = entryNode?.Icon;
            string name = GetQuestDisplayName(questGraph);
            string description = GetQuestDescription(questGraph);
            DrawPreviewCard(header, sprite, name, description);
        }

        public static void DrawQuestNodePreview(QuestNodeData nodeData, string header = "Quest Node Preview")
        {
            if (nodeData == null)
            {
                return;
            }

            string title = GetNodeEditorTitle(nodeData);
            string localizedName = GetNodeDisplayName(nodeData);
            string localizedDescription = GetNodeDescription(nodeData);
            DrawPreviewCard(header, nodeData.Icon, title, localizedName, localizedDescription);
        }

        public static void DrawQuestTransitionPreview(QuestGraph questGraph, QuestTransition transition, string header = "Transition Preview")
        {
            if (questGraph == null || transition == null)
            {
                return;
            }

            QuestNode ownerNode = FindTransitionOwner(questGraph, transition);

            string sourceName = ownerNode?.NodeData != null
                ? GetNodeEditorTitle(ownerNode.NodeData)
                : "Unknown";
            string targetName = transition.TargetNode != null
                ? GetNodeEditorTitle(transition.TargetNode)
                : "Missing Target";

            DrawPreviewCard(header, transition.TargetNode?.Icon, $"{sourceName} -> {targetName}", string.Empty);
        }

        public static string GetLocalizedStringDisplayName(SerializedProperty localizedStringProperty, string fallbackName)
        {
            if (localizedStringProperty == null)
            {
                return $"No string: {fallbackName}";
            }

            SerializedProperty tableReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableReference");
            SerializedProperty tableCollectionNameProperty = tableReferenceProperty?.FindPropertyRelative("m_TableCollectionName");
            SerializedProperty entryReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableEntryReference");
            SerializedProperty keyProperty = entryReferenceProperty?.FindPropertyRelative("m_Key");
            SerializedProperty keyIdProperty = entryReferenceProperty?.FindPropertyRelative("m_KeyId");

            StringTableCollection collection = ResolveCollection(tableCollectionNameProperty?.stringValue);
            if (collection == null)
            {
                return GetFallbackEntryLabel(keyProperty, keyIdProperty, fallbackName);
            }

            SharedTableData.SharedTableEntry entry = ResolveEntry(collection, keyIdProperty, keyProperty);
            if (entry == null)
            {
                return GetFallbackEntryLabel(keyProperty, keyIdProperty, fallbackName);
            }

            string localizedValue = GetLocalizedValue(collection, entry.Id, PreferredPreviewLocale);
            if (!string.IsNullOrWhiteSpace(localizedValue))
            {
                return localizedValue;
            }

            if (!string.IsNullOrWhiteSpace(entry.Key))
            {
                return entry.Key;
            }

            return $"Key {entry.Id}";
        }

        private static string GetLocalizedStringPreviewValue(SerializedProperty localizedStringProperty)
        {
            if (localizedStringProperty == null)
            {
                return string.Empty;
            }

            SerializedProperty tableReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableReference");
            SerializedProperty tableCollectionNameProperty = tableReferenceProperty?.FindPropertyRelative("m_TableCollectionName");
            SerializedProperty entryReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableEntryReference");
            SerializedProperty keyProperty = entryReferenceProperty?.FindPropertyRelative("m_Key");
            SerializedProperty keyIdProperty = entryReferenceProperty?.FindPropertyRelative("m_KeyId");

            StringTableCollection collection = ResolveCollection(tableCollectionNameProperty?.stringValue);
            if (collection == null)
            {
                return string.Empty;
            }

            SharedTableData.SharedTableEntry entry = ResolveEntry(collection, keyIdProperty, keyProperty);
            if (entry == null)
            {
                return string.Empty;
            }

            string localizedValue = GetLocalizedValue(collection, entry.Id, PreferredPreviewLocale);
            return !string.IsNullOrWhiteSpace(localizedValue)
                ? localizedValue
                : entry.Key ?? string.Empty;
        }

        private static string GetFallbackEntryLabel(SerializedProperty keyProperty, SerializedProperty keyIdProperty, string fallbackName)
        {
            if (keyProperty != null && !string.IsNullOrWhiteSpace(keyProperty.stringValue))
            {
                return keyProperty.stringValue;
            }

            if (keyIdProperty != null && keyIdProperty.longValue != 0)
            {
                return $"Key {keyIdProperty.longValue}";
            }

            return $"No string: {fallbackName}";
        }

        private static StringTableCollection ResolveCollection(string serializedTableReference)
        {
            return GraphEditorLocalizationCache.ResolveStringTableCollection(serializedTableReference);
        }

        private static SharedTableData.SharedTableEntry ResolveEntry(
            StringTableCollection collection,
            SerializedProperty keyIdProperty,
            SerializedProperty keyProperty)
        {
            if (collection == null)
            {
                return null;
            }

            if (keyIdProperty != null && keyIdProperty.longValue != 0)
            {
                SharedTableData.SharedTableEntry entryById = collection.SharedData.GetEntry(keyIdProperty.longValue);
                if (entryById != null)
                {
                    return entryById;
                }
            }

            if (keyProperty != null && !string.IsNullOrWhiteSpace(keyProperty.stringValue))
            {
                return collection.SharedData.GetEntry(keyProperty.stringValue);
            }

            return null;
        }

        private static string GetLocalizedValue(StringTableCollection collection, long entryId, string localeCode)
        {
            return GraphEditorLocalizationCache.GetLocalizedValue(collection, entryId, localeCode);
        }

        private static SerializedObject GetSerializedNodeObject(QuestNodeData nodeData)
        {
            if (!serializedNodeObjects.TryGetValue(nodeData, out SerializedObject nodeObject))
            {
                nodeObject = new SerializedObject(nodeData);
                serializedNodeObjects[nodeData] = nodeObject;
            }

            nodeObject.Update();
            return nodeObject;
        }

        private static SerializedObject GetSerializedGraphObject(QuestGraph questGraph)
        {
            if (!serializedGraphObjects.TryGetValue(questGraph, out SerializedObject graphObject))
            {
                graphObject = new SerializedObject(questGraph);
                serializedGraphObjects[questGraph] = graphObject;
            }

            graphObject.Update();
            return graphObject;
        }

        private static QuestNode FindTransitionOwner(QuestGraph questGraph, QuestTransition transition)
        {
            if (questGraph?.Nodes == null)
            {
                return null;
            }

            foreach (QuestNode node in questGraph.Nodes)
            {
                if (node?.NodeData?.Transitions != null && node.NodeData.Transitions.Contains(transition))
                {
                    return node;
                }
            }

            return null;
        }

        private static void InvalidateCaches()
        {
            serializedNodeObjects.Clear();
            serializedGraphObjects.Clear();
        }

        private static void DrawPreviewCard(string header, Sprite sprite, string title, string description)
        {
            DrawPreviewCard(header, sprite, title, description, string.Empty);
        }

        private static void DrawPreviewCard(string header, Sprite sprite, string title, string subtitle, string description)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawSpritePreview(sprite, 64f);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(subtitle, EditorStyles.wordWrappedMiniLabel);
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Description", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void DrawSpritePreview(Sprite sprite, float size)
        {
            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));

            if (sprite == null || sprite.texture == null)
            {
                GUI.Label(rect, "No Sprite", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Rect textureRect = sprite.textureRect;
            textureRect.x /= sprite.texture.width;
            textureRect.width /= sprite.texture.width;
            textureRect.y /= sprite.texture.height;
            textureRect.height /= sprite.texture.height;
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, textureRect, true);
        }
    }
}
