using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dialogs.Graph.Model;
using Dialogue;
using EditorTools;
using Quests.Editor;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Dialogs.Graph.Editor
{
    /// <summary>Owns localized-string selection, preview, and editor-side lookup caches.</summary>
    public partial class DialogEditorWindow
    {
        private readonly struct CachedLocalizedEntryOptions
        {
            public static CachedLocalizedEntryOptions Empty { get; } =
                new(Array.Empty<SharedTableData.SharedTableEntry>(), new[] { "<None>" });

            public CachedLocalizedEntryOptions(IReadOnlyList<SharedTableData.SharedTableEntry> entries, string[] options)
            {
                Entries = entries;
                Options = options;
                IndicesById = new Dictionary<long, int>(entries.Count);
                IndicesByKey = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);
                for (int i = 0; i < entries.Count; i++)
                {
                    SharedTableData.SharedTableEntry entry = entries[i];
                    int optionIndex = i + 1;
                    IndicesById[entry.Id] = optionIndex;
                    if (!string.IsNullOrEmpty(entry.Key))
                    {
                        IndicesByKey[entry.Key] = optionIndex;
                    }
                }
            }

            public IReadOnlyList<SharedTableData.SharedTableEntry> Entries { get; }
            public string[] Options { get; }
            public Dictionary<long, int> IndicesById { get; }
            public Dictionary<string, int> IndicesByKey { get; }
        }


        private void DrawLocalizedStringSelector(SerializedProperty localizedStringProperty, string label)
        {
            if (localizedStringProperty == null)
            {
                return;
            }

            SerializedProperty tableReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableReference");
            SerializedProperty entryReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableEntryReference");
            if (tableReferenceProperty == null || entryReferenceProperty == null)
            {
                EditorGUILayout.PropertyField(localizedStringProperty, new GUIContent(label), true);
                return;
            }

            SerializedProperty tableCollectionNameProperty = tableReferenceProperty.FindPropertyRelative("m_TableCollectionName");
            SerializedProperty keyIdProperty = entryReferenceProperty.FindPropertyRelative("m_KeyId");
            SerializedProperty keyProperty = entryReferenceProperty.FindPropertyRelative("m_Key");
            if (tableCollectionNameProperty == null || keyIdProperty == null || keyProperty == null)
            {
                EditorGUILayout.PropertyField(localizedStringProperty, new GUIContent(label), true);
                return;
            }

            if (TryDrawLocalizedStringSearchPicker(localizedStringProperty, label))
            {
                DrawLocalizedStringPreview(localizedStringProperty);
                return;
            }

            var collections = GetCachedStringTableCollections();
            string currentTableValue = tableCollectionNameProperty.stringValue;
            int selectedCollectionIndex = GetSelectedCollectionIndex(collections, currentTableValue);

            EditorGUILayout.LabelField(label, BoldLabelStyle);

            int newCollectionIndex = DrawPopupField("Table", selectedCollectionIndex, GetCachedStringTableOptions());
            if (newCollectionIndex != selectedCollectionIndex)
            {
                ApplyCollectionSelection(tableCollectionNameProperty, keyIdProperty, keyProperty, collections, newCollectionIndex);
                selectedCollectionIndex = newCollectionIndex;
            }

            if (selectedCollectionIndex <= 0)
            {
                EditorGUILayout.HelpBox("Select a localization table.", MessageType.None);
                return;
            }

            StringTableCollection selectedCollection = collections[selectedCollectionIndex - 1];
            CachedLocalizedEntryOptions entryOptions = GetCachedLocalizedEntryOptions(selectedCollection);

            int selectedEntryIndex = GetSelectedEntryIndex(entryOptions, keyIdProperty.longValue, keyProperty.stringValue);
            string currentEntryLabel = selectedEntryIndex > 0 && selectedEntryIndex < entryOptions.Options.Length
                ? entryOptions.Options[selectedEntryIndex]
                : "<None>";

            Rect entryRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(entryRect, $"Entry: {currentEntryLabel}", PopupStyle))
            {
                LocalizedEntrySelectorWindow.Show(
                    entryRect,
                    localizedStringProperty.serializedObject.targetObject,
                    keyIdProperty.propertyPath,
                    keyProperty.propertyPath,
                    entryOptions.Entries,
                    selectedEntryIndex > 0 ? selectedEntryIndex - 1 : -1);
            }

            DrawLocalizedStringPreview(localizedStringProperty);
        }

        private static int GetSelectedCollectionIndex(System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> collections, string serializedTableReference)
        {
            if (string.IsNullOrEmpty(serializedTableReference))
            {
                return 0;
            }

            for (int i = 0; i < collections.Count; i++)
            {
                StringTableCollection collection = collections[i];
                string guidReference = $"GUID:{collection.SharedData.TableCollectionNameGuid:N}";
                if (string.Equals(serializedTableReference, guidReference, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(serializedTableReference, collection.TableCollectionName, StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private void DrawLocalizedStringPreview(SerializedProperty localizedStringProperty)
        {
            string previewText = GetLocalizedStringPreview(localizedStringProperty, PreferredPreviewLocale);
            if (string.IsNullOrWhiteSpace(previewText))
            {
                return;
            }

            EditorGUILayout.LabelField("RU Preview", MiniBoldLabelStyle);

            GUIStyle previewStyle = PreviewLabelStyle;

            float width = LocalizedPreviewWidth;
            localizedPreviewContent.text = previewText;
            float height = Mathf.Max(LocalizedPreviewMinHeight, previewStyle.CalcHeight(localizedPreviewContent, width));

            EditorGUILayout.BeginVertical(
                useLightTheme ? HelpBoxStyle : EditorStyles.helpBox,
                GUILayout.MinHeight(LocalizedPreviewMinHeight),
                GUILayout.Height(height));
            GUILayout.Label(
                previewText,
                previewStyle,
                GUILayout.MinHeight(LocalizedPreviewMinHeight),
                GUILayout.Height(height));
            EditorGUILayout.EndVertical();
        }

        private static string GetLocalizedStringPreview(SerializedProperty localizedStringProperty, string localeCode)
        {
            if (localizedStringProperty == null)
            {
                return string.Empty;
            }

            SerializedProperty tableReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableReference");
            SerializedProperty entryReferenceProperty = localizedStringProperty.FindPropertyRelative("m_TableEntryReference");
            SerializedProperty tableCollectionNameProperty = tableReferenceProperty?.FindPropertyRelative("m_TableCollectionName");
            SerializedProperty keyIdProperty = entryReferenceProperty?.FindPropertyRelative("m_KeyId");
            SerializedProperty keyProperty = entryReferenceProperty?.FindPropertyRelative("m_Key");

            StringTableCollection collection = ResolveStringTableCollection(tableCollectionNameProperty?.stringValue);
            if (collection == null)
            {
                return string.Empty;
            }

            SharedTableData.SharedTableEntry entry = ResolveSharedTableEntry(collection, keyIdProperty, keyProperty);
            if (entry == null)
            {
                return string.Empty;
            }

            return GetLocalizedValue(collection, entry.Id, localeCode);
        }

        private static StringTableCollection ResolveStringTableCollection(string serializedTableReference)
        {
            return GraphEditorLocalizationCache.ResolveStringTableCollection(serializedTableReference);
        }

        private static SharedTableData.SharedTableEntry ResolveSharedTableEntry(
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

        private static System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> GetCachedStringTableCollections()
        {
            cachedStringTableCollections ??= GraphEditorLocalizationCache.GetStringTableCollections();
            return cachedStringTableCollections;
        }

        private static string[] GetCachedStringTableOptions()
        {
            if (cachedStringTableOptions != null)
            {
                return cachedStringTableOptions;
            }

            var collections = GetCachedStringTableCollections();
            cachedStringTableOptions = new string[collections.Count + 1];
            cachedStringTableOptions[0] = "<None>";
            for (int i = 0; i < collections.Count; i++)
            {
                cachedStringTableOptions[i + 1] = collections[i].TableCollectionName;
            }

            return cachedStringTableOptions;
        }

        private static CachedLocalizedEntryOptions GetCachedLocalizedEntryOptions(StringTableCollection collection)
        {
            if (collection == null)
            {
                return CachedLocalizedEntryOptions.Empty;
            }

            string cacheKey = collection.SharedData != null
                ? collection.SharedData.TableCollectionNameGuid.ToString("N")
                : collection.TableCollectionName;

            if (localizedEntryOptionsCache.TryGetValue(cacheKey, out CachedLocalizedEntryOptions cachedOptions))
            {
                return cachedOptions;
            }

            IReadOnlyList<SharedTableData.SharedTableEntry> entries = collection.SharedData.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();

            string[] options = new string[entries.Count + 1];
            options[0] = "<None>";
            for (int i = 0; i < entries.Count; i++)
            {
                options[i + 1] = entries[i].Key;
            }

            cachedOptions = new CachedLocalizedEntryOptions(entries, options);
            localizedEntryOptionsCache[cacheKey] = cachedOptions;
            return cachedOptions;
        }

        private static int GetSelectedEntryIndex(CachedLocalizedEntryOptions entryOptions, long keyId, string keyName)
        {
            if (keyId != 0 && entryOptions.IndicesById.TryGetValue(keyId, out int indexById))
            {
                return indexById;
            }

            if (!string.IsNullOrEmpty(keyName) && entryOptions.IndicesByKey.TryGetValue(keyName, out int indexByKey))
            {
                return indexByKey;
            }

            return 0;
        }

        private static void ApplyCollectionSelection(
            SerializedProperty tableCollectionNameProperty,
            SerializedProperty keyIdProperty,
            SerializedProperty keyProperty,
            System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> collections,
            int selectedCollectionIndex)
        {
            if (selectedCollectionIndex <= 0)
            {
                tableCollectionNameProperty.stringValue = string.Empty;
                keyIdProperty.longValue = 0;
                keyProperty.stringValue = string.Empty;
                return;
            }

            StringTableCollection collection = collections[selectedCollectionIndex - 1];
            tableCollectionNameProperty.stringValue = $"GUID:{collection.SharedData.TableCollectionNameGuid:N}";
            keyIdProperty.longValue = 0;
            keyProperty.stringValue = string.Empty;
        }

        private static bool TryDrawLocalizedStringSearchPicker(SerializedProperty localizedStringProperty, string label)
        {
            #if ENABLE_SEARCH
            if (!CanUseLocalizedStringSearchPicker())
            {
                return false;
            }

            SerializedProperty tableProperty = localizedStringProperty.FindPropertyRelative("m_TableReference");
            SerializedProperty entryProperty = localizedStringProperty.FindPropertyRelative("m_TableEntryReference");
            if (tableProperty == null || entryProperty == null)
            {
                return false;
            }

            string currentSelectionLabel = GetLocalizedStringSelectionLabel(tableProperty, entryProperty);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);

            if (GUILayout.Button(currentSelectionLabel, PopupStyle))
            {
                if (TryOpenLocalizedStringSearchPicker(tableProperty, entryProperty))
                {
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
            return true;
            #else
            return false;
            #endif
        }

        private static bool CanUseLocalizedStringSearchPicker()
        {
            #if ENABLE_SEARCH
            Assembly localizationAssembly = typeof(LocalizationEditorSettings).Assembly;
            Type pickerTypeDefinition = localizationAssembly.GetType("UnityEditor.Localization.UI.LocalizedReferencePicker`1");
            if (pickerTypeDefinition == null)
            {
                return false;
            }

            return CreateStringTableSearchContext(localizationAssembly) != null;
            #else
            return false;
            #endif
        }

        private static string GetLocalizedStringSelectionLabel(SerializedProperty tableProperty, SerializedProperty entryProperty)
        {
            SerializedProperty tableCollectionNameProperty = tableProperty.FindPropertyRelative("m_TableCollectionName");
            SerializedProperty keyIdProperty = entryProperty.FindPropertyRelative("m_KeyId");
            SerializedProperty keyProperty = entryProperty.FindPropertyRelative("m_Key");

            string tableLabel = "<None>";
            string entryLabel = "<None>";

            if (tableCollectionNameProperty != null && !string.IsNullOrEmpty(tableCollectionNameProperty.stringValue))
            {
                string serializedTableReference = tableCollectionNameProperty.stringValue;
                foreach (StringTableCollection collection in GetCachedStringTableCollections())
                {
                    string guidReference = $"GUID:{collection.SharedData.TableCollectionNameGuid:N}";
                    if (string.Equals(serializedTableReference, guidReference, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(serializedTableReference, collection.TableCollectionName, StringComparison.Ordinal))
                    {
                        tableLabel = collection.TableCollectionName;

                        if (keyIdProperty != null && keyIdProperty.longValue != 0)
                        {
                            SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(keyIdProperty.longValue);
                            if (entry != null)
                            {
                                entryLabel = entry.Key;
                            }
                        }
                        else if (keyProperty != null && !string.IsNullOrEmpty(keyProperty.stringValue))
                        {
                            entryLabel = keyProperty.stringValue;
                        }

                        break;
                    }
                }
            }

            return $"{tableLabel}/{entryLabel}";
        }

        private static bool TryOpenLocalizedStringSearchPicker(SerializedProperty tableProperty, SerializedProperty entryProperty)
        {
            #if ENABLE_SEARCH
            Assembly localizationAssembly = typeof(LocalizationEditorSettings).Assembly;
            Type pickerTypeDefinition = localizationAssembly.GetType("UnityEditor.Localization.UI.LocalizedReferencePicker`1");
            if (pickerTypeDefinition == null)
            {
                return false;
            }

            Type pickerType = pickerTypeDefinition.MakeGenericType(typeof(StringTableCollection));
            object context = CreateStringTableSearchContext(localizationAssembly);
            if (context == null)
            {
                return false;
            }

            object picker = Activator.CreateInstance(pickerType, context, "string table entry", tableProperty, entryProperty);
            MethodInfo showMethod = pickerType.GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
            if (showMethod == null)
            {
                return false;
            }

            showMethod.Invoke(picker, null);
            return true;
            #else
            return false;
            #endif
        }

        private static object CreateStringTableSearchContext(Assembly localizationAssembly)
        {
            #if ENABLE_SEARCH
            #if UNITY_2022_3_OR_NEWER
            return UnityEditor.Search.SearchService.CreateContext(
                "st",
                "st:",
                UnityEditor.Search.SearchFlags.UseSessionSettings);
            #else
            Type providerType = localizationAssembly.GetType("UnityEditor.Localization.Search.StringTableSearchProvider");
            if (providerType == null)
            {
                return null;
            }

            var provider = Activator.CreateInstance(providerType) as UnityEditor.Search.SearchProvider;
            if (provider == null)
            {
                return null;
            }

            return UnityEditor.Search.SearchService.CreateContext(provider, "st:");
            #endif
            #else
            return null;
            #endif
        }

    }
}
