using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace EditorTools
{
    internal sealed class LocalizedEntrySelectorWindow : EditorWindow
    {
        [Serializable]
        internal struct EntryOption
        {
            public long Id;
            public string Key;
        }

        private static LocalizedEntrySelectorWindow activeWindow;

        [SerializeField] private UnityEngine.Object targetObject;
        [SerializeField] private string keyIdPropertyPath;
        [SerializeField] private string keyPropertyPath;
        [SerializeField] private List<EntryOption> entries = new();
        [SerializeField] private int selectedIndex;
        private Vector2 scrollPosition;
        private string searchText = string.Empty;
        private bool focusSearchField = true;
        [NonSerialized] private SearchField searchField;
        [NonSerialized] private readonly List<int> filteredEntryIndices = new();
        [NonSerialized] private string appliedSearchText;
        [NonSerialized] private ListView toolkitEntryList;
        [NonSerialized] private bool toolkitUiActive;

        private const float EntryRowHeight = 20f;

        private void Initialize(
            UnityEngine.Object targetObject,
            string keyIdPropertyPath,
            string keyPropertyPath,
            IReadOnlyList<SharedTableData.SharedTableEntry> entries,
            int selectedIndex)
        {
            this.targetObject = targetObject;
            this.keyIdPropertyPath = keyIdPropertyPath;
            this.keyPropertyPath = keyPropertyPath;
            this.entries = entries != null
                ? entries.Select(entry => new EntryOption { Id = entry.Id, Key = entry.Key }).ToList()
                : new List<EntryOption>();
            this.selectedIndex = selectedIndex;
            focusSearchField = true;
            searchText = string.Empty;
            scrollPosition = Vector2.zero;
            EnsureSearchField();
            RebuildFilteredEntries();
            RebuildToolkitUi();
        }

        private Vector2 InitialSize
        {
            get
            {
                float height = Mathf.Clamp(110f + Mathf.Min(entries.Count, 8) * 22f, 180f, 420f);
                return new Vector2(360f, height);
            }
        }

        private void OnEnable()
        {
            EnsureSearchField();
            RebuildFilteredEntries();
        }

        private void CreateGUI()
        {
            toolkitUiActive = true;
            RebuildToolkitUi();
        }

        private void OnGUI()
        {
            if (toolkitUiActive)
            {
                return;
            }

            EnsureSearchField();

            if (focusSearchField)
            {
                searchField.SetFocus();
                focusSearchField = false;
            }

            EditorGUILayout.LabelField("Select Entry", EditorStyles.boldLabel);
            string updatedSearchText = searchField.OnGUI(EditorGUILayout.GetControlRect(), searchText);
            if (!string.Equals(searchText, updatedSearchText, StringComparison.Ordinal))
            {
                searchText = updatedSearchText;
                scrollPosition = Vector2.zero;
                RebuildFilteredEntries();
            }

            EnsureFilteredEntries();
            EditorGUILayout.Space(4f);

            if (GUILayout.Button("<None>", selectedIndex < 0 ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
            {
                ApplySelection(0);
                Close();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space(4f);
            if (filteredEntryIndices.Count == 0)
            {
                EditorGUILayout.HelpBox("No entries found.", MessageType.Info);
                return;
            }

            DrawVirtualizedEntryList();
        }

        private void RebuildToolkitUi()
        {
            if (!toolkitUiActive)
            {
                return;
            }

            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 7f;
            rootVisualElement.style.paddingRight = 7f;
            rootVisualElement.style.paddingTop = 7f;
            rootVisualElement.style.paddingBottom = 7f;

            var title = new Label("Select Entry");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4f;
            rootVisualElement.Add(title);

            var search = new ToolbarSearchField { value = searchText ?? string.Empty };
            search.RegisterValueChangedCallback(evt =>
            {
                if (string.Equals(searchText, evt.newValue, StringComparison.Ordinal))
                {
                    return;
                }

                searchText = evt.newValue;
                RebuildFilteredEntries();
                toolkitEntryList?.RefreshItems();
            });
            rootVisualElement.Add(search);

            var noneButton = new Button(() =>
            {
                ApplySelection(0);
                Close();
            })
            {
                text = "<None>"
            };
            noneButton.style.marginTop = 4f;
            noneButton.style.marginBottom = 4f;
            rootVisualElement.Add(noneButton);

            toolkitEntryList = new ListView
            {
                fixedItemHeight = EntryRowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                itemsSource = filteredEntryIndices
            };
            toolkitEntryList.style.flexGrow = 1f;
            toolkitEntryList.style.minHeight = 64f;
            toolkitEntryList.makeItem = () =>
            {
                var button = new Button();
                button.style.height = EntryRowHeight;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.currentTarget is not Button entryButton || entryButton.userData is not int entryIndex)
                    {
                        return;
                    }

                    ApplySelection(entryIndex + 1);
                    Close();
                });
                return button;
            };
            toolkitEntryList.bindItem = (element, index) =>
            {
                int entryIndex = filteredEntryIndices[index];
                var button = (Button)element;
                button.text = entries[entryIndex].Key;
                button.userData = entryIndex;
                button.style.backgroundColor = entryIndex == selectedIndex
                    ? new Color(0.28f, 0.42f, 0.58f, 0.85f)
                    : StyleKeyword.Null;
            };
            rootVisualElement.Add(toolkitEntryList);

            if (focusSearchField)
            {
                rootVisualElement.schedule.Execute(() => search.Focus()).ExecuteLater(0);
                focusSearchField = false;
            }
        }

        public static void Show(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string keyIdPropertyPath,
            string keyPropertyPath,
            IReadOnlyList<SharedTableData.SharedTableEntry> entries,
            int selectedIndex)
        {
            activeWindow?.Close();

            var window = CreateInstance<LocalizedEntrySelectorWindow>();
            window.Initialize(targetObject, keyIdPropertyPath, keyPropertyPath, entries, selectedIndex);
            window.titleContent = new GUIContent("Select Entry");
            window.minSize = new Vector2(320f, 180f);

            Vector2 initialSize = window.InitialSize;
            Rect anchorRect = GetCursorRect(activatorRect);
            window.position = new Rect(anchorRect.x, anchorRect.y, initialSize.x, initialSize.y);
            window.Show();
            window.Focus();

            activeWindow = window;
        }

        private void ApplySelection(int selectedEntryIndex)
        {
            ApplyEntrySelectionToObject(
                targetObject,
                keyIdPropertyPath,
                keyPropertyPath,
                entries,
                selectedEntryIndex);
        }

        private void OnDestroy()
        {
            toolkitUiActive = false;
            if (activeWindow == this)
            {
                activeWindow = null;
            }
        }

        private void EnsureSearchField()
        {
            searchField ??= new SearchField();
        }

        private void EnsureFilteredEntries()
        {
            if (!string.Equals(appliedSearchText, searchText, StringComparison.Ordinal))
            {
                RebuildFilteredEntries();
            }
        }

        private void RebuildFilteredEntries()
        {
            filteredEntryIndices.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                if (MatchesSearch(entries[i], searchText))
                {
                    filteredEntryIndices.Add(i);
                }
            }

            appliedSearchText = searchText;
        }

        private void DrawVirtualizedEntryList()
        {
            Rect scrollRect = GUILayoutUtility.GetRect(
                1f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(64f));
            float contentWidth = Mathf.Max(1f, scrollRect.width - GUI.skin.verticalScrollbar.fixedWidth);
            float contentHeight = Mathf.Max(scrollRect.height, filteredEntryIndices.Count * EntryRowHeight);
            Rect contentRect = new Rect(0f, 0f, contentWidth, contentHeight);
            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, contentRect);

            int firstVisibleIndex = Mathf.Clamp(Mathf.FloorToInt(scrollPosition.y / EntryRowHeight), 0, filteredEntryIndices.Count - 1);
            int visibleCount = Mathf.CeilToInt(scrollRect.height / EntryRowHeight) + 2;
            int lastVisibleIndex = Mathf.Min(filteredEntryIndices.Count, firstVisibleIndex + visibleCount);
            for (int filteredIndex = firstVisibleIndex; filteredIndex < lastVisibleIndex; filteredIndex++)
            {
                int entryIndex = filteredEntryIndices[filteredIndex];
                EntryOption entry = entries[entryIndex];
                Rect entryRect = new Rect(0f, filteredIndex * EntryRowHeight, contentWidth, EntryRowHeight);
                GUIStyle style = entryIndex == selectedIndex ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (GUI.Button(entryRect, entry.Key, style))
                {
                    ApplySelection(entryIndex + 1);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }

            GUI.EndScrollView();
        }

        private static bool MatchesSearch(EntryOption entry, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            return entry.Key?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Rect GetCursorRect(Rect fallbackRect)
        {
            Vector2 screenPoint;
            if (Event.current != null)
            {
                screenPoint = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
            else
            {
                screenPoint = GUIUtility.GUIToScreenPoint(new Vector2(fallbackRect.xMax, fallbackRect.yMax));
            }

            return new Rect(screenPoint.x + 12f, screenPoint.y + 12f, 1f, 1f);
        }

        private static void ApplyEntrySelectionToObject(
            UnityEngine.Object targetObject,
            string keyIdPropertyPath,
            string keyPropertyPath,
            IReadOnlyList<EntryOption> entries,
            int selectedEntryIndex)
        {
            if (targetObject == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(targetObject);
            SerializedProperty keyIdProperty = serializedObject.FindProperty(keyIdPropertyPath);
            SerializedProperty keyProperty = serializedObject.FindProperty(keyPropertyPath);
            if (keyIdProperty == null || keyProperty == null)
            {
                return;
            }

            if (selectedEntryIndex <= 0)
            {
                keyIdProperty.longValue = 0;
                keyProperty.stringValue = string.Empty;
            }
            else
            {
                EntryOption entry = entries[selectedEntryIndex - 1];
                keyIdProperty.longValue = entry.Id;
                keyProperty.stringValue = string.Empty;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
        }
    }


}
