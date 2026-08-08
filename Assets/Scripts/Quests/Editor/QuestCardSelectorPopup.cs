using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Quests.Editor
{
    public sealed class QuestCardSelectorPopup : EditorWindow
    {
        private static QuestCardSelectorPopup activeWindow;

        public sealed class Entry
        {
            public string Title;
            public string Subtitle;
            public Sprite Sprite;
            public bool IsSelected;
            public Action OnSelect;
        }

        private string header;
        private List<Entry> entries = new();
        private Vector2 scrollPosition;
        private bool toolkitUiActive;

        private void Initialize(string header, List<Entry> entries)
        {
            this.header = header;
            this.entries = entries ?? new List<Entry>();
            scrollPosition = Vector2.zero;
            RebuildToolkitUi();
        }

        private void CreateGUI()
        {
            toolkitUiActive = true;
            RebuildToolkitUi();
        }

        private Vector2 InitialSize
        {
            get
            {
                float height = Mathf.Clamp(56f + entries.Count * 82f, 180f, 520f);
                return new Vector2(420f, height);
            }
        }

        private void OnGUI()
        {
            if (toolkitUiActive)
            {
                return;
            }

            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (Entry entry in entries)
            {
                DrawEntry(entry);
            }

            EditorGUILayout.EndScrollView();
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

            var title = new Label(header ?? string.Empty);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5f;
            rootVisualElement.Add(title);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1f;
            foreach (Entry entry in entries)
            {
                scrollView.Add(CreateToolkitEntry(entry));
            }

            rootVisualElement.Add(scrollView);
        }

        private Button CreateToolkitEntry(Entry entry)
        {
            var button = new Button(() =>
            {
                entry.OnSelect?.Invoke();
                Close();
            });
            button.style.height = 72f;
            button.style.marginBottom = 4f;
            button.style.paddingLeft = 8f;
            button.style.paddingRight = 8f;
            button.style.paddingTop = 8f;
            button.style.paddingBottom = 8f;
            button.style.flexDirection = FlexDirection.Row;
            button.style.backgroundColor = entry.IsSelected
                ? new Color(0.28f, 0.42f, 0.58f, 0.85f)
                : new Color(0.22f, 0.22f, 0.22f, 1f);

            var image = new Image
            {
                sprite = entry.Sprite,
                scaleMode = ScaleMode.ScaleToFit
            };
            image.style.width = 56f;
            image.style.minWidth = 56f;
            image.style.height = 56f;
            image.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
            button.Add(image);

            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1f;
            textContainer.style.marginLeft = 8f;
            textContainer.style.flexDirection = FlexDirection.Column;
            var entryTitle = new Label(entry.Title ?? string.Empty);
            entryTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            entryTitle.style.whiteSpace = WhiteSpace.NoWrap;
            entryTitle.style.overflow = Overflow.Hidden;
            entryTitle.style.textOverflow = TextOverflow.Ellipsis;
            textContainer.Add(entryTitle);
            var subtitle = new Label(entry.Subtitle ?? string.Empty);
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.fontSize = 10f;
            subtitle.style.opacity = 0.82f;
            textContainer.Add(subtitle);
            button.Add(textContainer);
            return button;
        }

        public static void Show(Rect activatorRect, string header, List<Entry> entries)
        {
            activeWindow?.Close();

            var window = CreateInstance<QuestCardSelectorPopup>();
            window.Initialize(header, entries);
            window.titleContent = new GUIContent(header);
            window.minSize = new Vector2(320f, 180f);

            Vector2 initialSize = window.InitialSize;
            Rect anchorRect = GetCursorRect(activatorRect);
            window.position = new Rect(anchorRect.x, anchorRect.y, initialSize.x, initialSize.y);
            window.Show();
            window.Focus();

            activeWindow = window;
        }

        private void DrawEntry(Entry entry)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 72f, GUILayout.ExpandWidth(true));
            Rect backgroundRect = new Rect(rect.x, rect.y, rect.width, rect.height);

            Color backgroundColor = entry.IsSelected
                ? new Color(0.28f, 0.42f, 0.58f, 0.85f)
                : new Color(0.22f, 0.22f, 0.22f, 1f);

            EditorGUI.DrawRect(backgroundRect, backgroundColor);
            GUI.Box(backgroundRect, GUIContent.none, EditorStyles.helpBox);

            Rect spriteRect = new Rect(backgroundRect.x + 8f, backgroundRect.y + 8f, 56f, 56f);
            DrawSprite(spriteRect, entry.Sprite);

            Rect titleRect = new Rect(spriteRect.xMax + 8f, backgroundRect.y + 8f, backgroundRect.width - 84f, 20f);
            Rect subtitleRect = new Rect(spriteRect.xMax + 8f, backgroundRect.y + 30f, backgroundRect.width - 84f, 34f);

            EditorGUI.LabelField(titleRect, entry.Title, EditorStyles.boldLabel);
            EditorGUI.LabelField(subtitleRect, entry.Subtitle, EditorStyles.wordWrappedMiniLabel);

            if (GUI.Button(backgroundRect, GUIContent.none, GUIStyle.none))
            {
                entry.OnSelect?.Invoke();
                Close();
                GUIUtility.ExitGUI();
            }
        }

        private void OnDestroy()
        {
            toolkitUiActive = false;
            if (activeWindow == this)
            {
                activeWindow = null;
            }
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
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
    }
}
