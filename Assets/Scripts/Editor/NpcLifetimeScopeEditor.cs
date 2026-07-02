using System.Collections.Generic;
using System.Linq;
using Container;
using Factions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace EditorTools
{
    [CustomEditor(typeof(NpcLifetimeScope))]
    public sealed class NpcLifetimeScopeEditor : Editor
    {
        private const string PreferredPreviewLocale = "ru";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((NpcLifetimeScope)target), typeof(NpcLifetimeScope), false);
            }

            DrawFactionPopup();
            EditorGUILayout.Space(4f);

            DrawPropertiesExcluding(serializedObject, "m_Script", "faction");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFactionPopup()
        {
            var factionProperty = serializedObject.FindProperty("faction");
            if (factionProperty == null)
            {
                return;
            }

            var factions = FindAllFactions();
            var labels = new List<string> { "None" };
            labels.AddRange(factions.Select(GetFactionDisplayName));

            var currentFaction = factionProperty.objectReferenceValue as FactionConfig;
            var selectedIndex = currentFaction == null ? 0 : factions.IndexOf(currentFaction) + 1;
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            var nextIndex = EditorGUILayout.Popup("Faction", selectedIndex, labels.ToArray());
            factionProperty.objectReferenceValue = nextIndex <= 0 ? null : factions[nextIndex - 1];
        }

        private static List<FactionConfig> FindAllFactions()
        {
            return AssetDatabase.FindAssets("t:FactionConfig")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FactionConfig>)
                .Where(faction => faction != null)
                .OrderBy(GetFactionDisplayName)
                .ToList();
        }

        private static string GetFactionDisplayName(FactionConfig faction)
        {
            if (faction == null)
            {
                return "Missing faction";
            }

            var factionObject = new SerializedObject(faction);
            var nameProperty = factionObject.FindProperty("<Name>k__BackingField");
            return GetLocalizedStringDisplayName(nameProperty, faction.name);
        }

        private static string GetLocalizedStringDisplayName(SerializedProperty localizedStringProperty, string fallbackName)
        {
            if (localizedStringProperty == null)
            {
                return fallbackName;
            }

            var tableReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableReference");
            var tableCollectionNameProperty = tableReferenceProperty?.FindPropertyRelative("m_TableCollectionName");
            var entryReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableEntryReference");
            var keyProperty = entryReferenceProperty?.FindPropertyRelative("m_Key");
            var keyIdProperty = entryReferenceProperty?.FindPropertyRelative("m_KeyId");

            var collection = ResolveCollection(tableCollectionNameProperty?.stringValue);
            if (collection == null)
            {
                return GetFallbackEntryLabel(keyProperty, keyIdProperty, fallbackName);
            }

            var entry = ResolveEntry(collection, keyIdProperty, keyProperty);
            if (entry == null)
            {
                return GetFallbackEntryLabel(keyProperty, keyIdProperty, fallbackName);
            }

            var localizedValue = GetLocalizedValue(collection, entry.Id, PreferredPreviewLocale);
            if (!string.IsNullOrWhiteSpace(localizedValue))
            {
                return localizedValue;
            }

            return !string.IsNullOrWhiteSpace(entry.Key) ? entry.Key : $"Key {entry.Id}";
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

            return fallbackName;
        }

        private static StringTableCollection ResolveCollection(string serializedTableReference)
        {
            if (string.IsNullOrWhiteSpace(serializedTableReference))
            {
                return null;
            }

            foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
            {
                var guidReference = $"GUID:{collection.SharedData.TableCollectionNameGuid:N}";
                if (string.Equals(serializedTableReference, guidReference, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(serializedTableReference, collection.TableCollectionName, System.StringComparison.Ordinal))
                {
                    return collection;
                }
            }

            return null;
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
                var entryById = collection.SharedData.GetEntry(keyIdProperty.longValue);
                if (entryById != null)
                {
                    return entryById;
                }
            }

            return keyProperty != null && !string.IsNullOrWhiteSpace(keyProperty.stringValue)
                ? collection.SharedData.GetEntry(keyProperty.stringValue)
                : null;
        }

        private static string GetLocalizedValue(StringTableCollection collection, long entryId, string localeCode)
        {
            if (collection == null || entryId == 0 || string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            foreach (var table in collection.StringTables)
            {
                if (table == null || table.LocaleIdentifier.Code != localeCode)
                {
                    continue;
                }

                var entry = table.GetEntry(entryId);
                if (entry != null && !string.IsNullOrWhiteSpace(entry.LocalizedValue))
                {
                    return entry.LocalizedValue;
                }
            }

            return string.Empty;
        }
    }
}
