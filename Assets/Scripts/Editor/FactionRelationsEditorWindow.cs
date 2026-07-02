using System.Collections.Generic;
using System.Linq;
using Factions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace EditorTools
{
    public sealed class FactionRelationsEditorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/RPG/Factions/Faction Relations";
        private const string FactionsFolderPath = "Assets/Configs/Factions";
        private const string RelationsConfigPath = FactionsFolderPath + "/FactionRelationsConfig.asset";
        private const string PreferredPreviewLocale = "ru";
        private const float FactionColumnWidth = 220f;
        private const float RelationColumnWidth = 90f;
        private const float RowHeight = 22f;

        private readonly List<FactionConfig> factions = new();
        private FactionRelationsConfig relationsConfig;
        private Vector2 relationsScroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<FactionRelationsEditorWindow>("Faction Relations");
            window.minSize = new Vector2(640f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            relationsConfig = FindFirstRelationsConfig();
            RefreshFactions();
            SyncRelations();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(6f);

            if (relationsConfig == null)
            {
                EditorGUILayout.HelpBox("Create or assign a FactionRelationsConfig to edit faction relations.", MessageType.Info);
                return;
            }

            if (factions.Count == 0)
            {
                EditorGUILayout.HelpBox("No FactionConfig assets found. Create factions in Assets/Configs/Factions.", MessageType.Info);
                return;
            }

            DrawRelationThresholds();
            EditorGUILayout.Space(8f);
            DrawRelationsTable();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                relationsConfig = (FactionRelationsConfig)EditorGUILayout.ObjectField(
                    relationsConfig,
                    typeof(FactionRelationsConfig),
                    false,
                    GUILayout.MinWidth(220f));

                if (GUILayout.Button("Create Config", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                {
                    relationsConfig = CreateRelationsConfig();
                    SyncRelations();
                }

                if (GUILayout.Button("Find Config", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                {
                    relationsConfig = FindFirstRelationsConfig();
                    SyncRelations();
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    RefreshFactions();
                    SyncRelations();
                }

                if (GUILayout.Button("Create Faction", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                {
                    CreateFaction();
                    RefreshFactions();
                    SyncRelations();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRelationThresholds()
        {
            var serializedConfig = new SerializedObject(relationsConfig);
            serializedConfig.Update();

            EditorGUILayout.LabelField("Relation Thresholds", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("hostileBelowRelation"), new GUIContent("Hostile Below"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("friendlyAboveRelation"), new GUIContent("Friendly Above"));

            if (serializedConfig.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(relationsConfig);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawRelationsTable()
        {
            EditorGUILayout.LabelField("Base Relations", EditorStyles.boldLabel);

            relationsScroll = EditorGUILayout.BeginScrollView(relationsScroll, GUILayout.MinHeight(260f));
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawHeaderCell("Faction", FactionColumnWidth);
                DrawHeaderCell("Relation", RelationColumnWidth);
                DrawHeaderCell("Faction", FactionColumnWidth);
            }

            EditorGUI.BeginChangeCheck();
            foreach (var entry in relationsConfig.Relations.Where(entry => entry != null))
            {
                var leftFaction = entry.LeftFaction;
                var rightFaction = entry.RightFaction;

                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(RowHeight)))
                {
                    EditorGUILayout.SelectableLabel(GetFactionDisplayName(leftFaction), EditorStyles.label, GUILayout.Width(FactionColumnWidth), GUILayout.Height(RowHeight));

                    var value = entry.Relation;
                    var nextValue = EditorGUILayout.IntField(value, GUILayout.Width(RelationColumnWidth), GUILayout.Height(RowHeight));
                    if (nextValue != value)
                    {
                        Undo.RecordObject(relationsConfig, "Change Faction Relation");
                        relationsConfig.SetRelation(leftFaction, rightFaction, nextValue);
                        EditorUtility.SetDirty(relationsConfig);
                    }

                    EditorGUILayout.SelectableLabel(GetFactionDisplayName(rightFaction), EditorStyles.label, GUILayout.Width(FactionColumnWidth), GUILayout.Height(RowHeight));
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeaderCell(string text, float width)
        {
            GUILayout.Label(text, EditorStyles.miniBoldLabel, GUILayout.Width(width), GUILayout.Height(RowHeight));
        }

        private void RefreshFactions()
        {
            factions.Clear();
            factions.AddRange(FindAllFactions());
        }

        private void SyncRelations()
        {
            if (relationsConfig == null)
            {
                return;
            }

            if (!relationsConfig.SyncWithFactions(factions))
            {
                return;
            }

            EditorUtility.SetDirty(relationsConfig);
            AssetDatabase.SaveAssets();
        }

        private static IReadOnlyList<FactionConfig> FindAllFactions()
        {
            return AssetDatabase.FindAssets("t:FactionConfig")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FactionConfig>)
                .Where(faction => faction != null)
                .OrderBy(faction => faction.name)
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

        private static FactionRelationsConfig FindFirstRelationsConfig()
        {
            return AssetDatabase.FindAssets("t:FactionRelationsConfig")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FactionRelationsConfig>)
                .FirstOrDefault(config => config != null);
        }

        private static FactionRelationsConfig CreateRelationsConfig()
        {
            EnsureFolder(FactionsFolderPath);

            var existing = AssetDatabase.LoadAssetAtPath<FactionRelationsConfig>(RelationsConfigPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                return existing;
            }

            var config = CreateInstance<FactionRelationsConfig>();
            AssetDatabase.CreateAsset(config, RelationsConfigPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            return config;
        }

        private static void CreateFaction()
        {
            EnsureFolder(FactionsFolderPath);
            var asset = CreateInstance<FactionConfig>();
            var path = AssetDatabase.GenerateUniqueAssetPath(FactionsFolderPath + "/FactionConfig.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
