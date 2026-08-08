using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dialogs.Graph.Model;
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
using UnityEngine.UIElements;

namespace Dialogs.Graph.Editor
{
    public class DialogEditorWindow : EditorWindow
    {
        private const string PreferredPreviewLocale = "ru";
        private const string DialogsPathKey = "DialogEditor_DialogsPath";
        private const string PhrasesPathKey = "DialogEditor_PhrasesPath";
        private const string ThemeKey = "DialogEditor_Theme";
        private const float DialogNodeWidth = 320f;
        private const float LocalizedPreviewMinHeight = 48f;
        private const float LocalizedPreviewWidth = 268f;
        private const float WorkspaceWidth = 10000f;
        private const float WorkspaceHeight = 10000f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 2f;
        private const float OverlayPanelWidth = 320f;
        private const float AccentLineWidth = 1.5f;

        private DialogGraph currentGraph;
        private Vector2 scrollPos;
        private string dialogsFolderPath;
        private string phrasesFolderPath;
        private readonly Dictionary<DialogAnswer, Vector2> answerAnchorPositions = new();
        private readonly Dictionary<DialogNode, Rect> nodeRects = new();
        private readonly HashSet<DialogNode> graphNodeSet = new();
        private readonly List<DialogNode> staleNodeRects = new();
        private readonly Dictionary<DialogPhrase, DialogNode> phraseToNodeLookup = new();
        private readonly HashSet<DialogPhrase> orphanPhrases = new();
        private readonly Dictionary<DialogPhrase, string> phraseDisplayNameCache = new();
        private readonly Dictionary<DialogPhrase, bool> restoreExitAbilityCache = new();
        private readonly HashSet<DialogPhrase> phrasesWithDirtyLayout = new();
        private readonly HashSet<DialogPhrase> phrasesAwaitingRepaintAfterLayout = new();
        private readonly GUIContent localizedPreviewContent = new();
        private readonly Dictionary<string, bool> answerFoldoutStates = new();
        private bool graphCachesDirty = true;
        private bool graphStructureDirty = true;
        private readonly Dictionary<DialogAnswer, CachedConnectionRoute> connectionRouteCache = new();
        private readonly List<Rect> connectionObstacleRects = new();
        private int connectionLayoutVersion;

        private static System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> cachedStringTableCollections;
        private static string[] cachedStringTableOptions;
        private static readonly Dictionary<string, CachedLocalizedEntryOptions> localizedEntryOptionsCache = new();
        private static List<QuestGraph> cachedQuestGraphs;
        private static List<QuestNodeData> cachedQuestSourceNodes;
        private static List<QuestNodeData> cachedTerminalQuestNodes;

        private bool isSelectingTargetPhrase;
        private bool isControlsPanelExpanded = true;
        private DialogAnswer pendingAnswer;
        private DialogPhrase sourcePhraseForSelection;
        private DialogNode activeConnectionNode;
        private readonly List<EditorStyleTextOverride> editorStyleTextOverrides = new();
        private GUIStyle lightWindowStyle;
        private GUIStyle lightHelpBoxStyle;
        private GUIStyle lightButtonStyle;
        private GUIStyle lightMiniButtonStyle;
        private GUIStyle lightPopupStyle;
        private GUIStyle lightTextFieldStyle;
        private GUIStyle lightLabelStyle;
        private GUIStyle lightFoldoutStyle;
        private GUIStyle lightBoldLabelStyle;
        private GUIStyle lightMiniBoldLabelStyle;
        private GUIStyle lightMiniLabelStyle;
        private GUIStyle lightWordWrappedMiniLabelStyle;
        private GUIStyle lightCenteredMiniLabelStyle;
        private GUIStyle lightPreviewLabelStyle;
        private Texture2D lightWindowTexture;
        private Texture2D lightHelpBoxTexture;
        private Texture2D lightButtonTexture;
        private Texture2D lightButtonHoverTexture;
        private Texture2D lightButtonActiveTexture;
        private Texture2D lightTextFieldTexture;
        private GUISkin lightSkin;

        private float zoom = 1f;
        private Vector2 panOffset = Vector2.zero;
        private bool useLightTheme;
        private DialogToolkitCanvas toolkitCanvas;
        private IMGUIContainer toolkitControls;
        private bool toolkitUiActive;

        [MenuItem("Tools/Dialog Editor")]
        public static void Open()
        {
            GetWindow<DialogEditorWindow>("Dialog Editor");
        }

        private void CreateGUI()
        {
            toolkitUiActive = true;
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.backgroundColor = WindowBackgroundColor;

            toolkitCanvas = new DialogToolkitCanvas(this);
            rootVisualElement.Add(toolkitCanvas);

            toolkitControls = new IMGUIContainer(DrawToolkitControls)
            {
                name = "dialog-editor-controls"
            };
            toolkitControls.style.position = Position.Absolute;
            toolkitControls.style.left = 0f;
            toolkitControls.style.top = 0f;
            toolkitControls.style.width = OverlayPanelWidth;
            toolkitControls.style.bottom = 0f;
            rootVisualElement.Add(toolkitControls);

            toolkitCanvas.RebuildNow();
        }

        private void OnEnable()
        {
            dialogsFolderPath = EditorPrefs.GetString(DialogsPathKey, "Assets/Dialogs");
            phrasesFolderPath = EditorPrefs.GetString(PhrasesPathKey, "Assets/DialogPhrases");
            useLightTheme = EditorPrefs.GetBool(ThemeKey, false);
            EditorApplication.projectChanged += HandleProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
            toolkitUiActive = false;
            toolkitCanvas = null;
            toolkitControls = null;
        }

        private void OnGUI()
        {
            if (toolkitUiActive)
            {
                return;
            }

            Color previousBackgroundColor = GUI.backgroundColor;
            Color previousContentColor = GUI.contentColor;
            GUISkin previousSkin = GUI.skin;

            try
            {
                ApplyThemeGuiColors();
                ApplyThemeSkin();
                ApplyThemeEditorStyleTextOverrides();
                DrawWindowBackground();

                if (currentGraph == null)
                {
                    DrawEmptyState();
                    DrawControlsOverlay();
                    return;
                }

                DrawGraphArea();
                DrawControlsOverlay();
            }
            finally
            {
                RestoreThemeEditorStyleTextOverrides();
                GUI.skin = previousSkin;
                GUI.backgroundColor = previousBackgroundColor;
                GUI.contentColor = previousContentColor;
            }
        }

        private void DrawToolkitControls()
        {
            Color previousBackgroundColor = GUI.backgroundColor;
            Color previousContentColor = GUI.contentColor;
            GUISkin previousSkin = GUI.skin;

            try
            {
                ApplyThemeGuiColors();
                ApplyThemeSkin();
                ApplyThemeEditorStyleTextOverrides();
                DrawControlsOverlay();
            }
            finally
            {
                RestoreThemeEditorStyleTextOverrides();
                GUI.skin = previousSkin;
                GUI.backgroundColor = previousBackgroundColor;
                GUI.contentColor = previousContentColor;
            }
        }

        private void DrawToolkitNode(DialogNode node)
        {
            Color previousBackgroundColor = GUI.backgroundColor;
            Color previousContentColor = GUI.contentColor;
            GUISkin previousSkin = GUI.skin;

            try
            {
                ApplyThemeGuiColors();
                ApplyThemeSkin();
                ApplyThemeEditorStyleTextOverrides();
                DrawNodeWindow(node, false);
            }
            finally
            {
                RestoreThemeEditorStyleTextOverrides();
                GUI.skin = previousSkin;
                GUI.backgroundColor = previousBackgroundColor;
                GUI.contentColor = previousContentColor;
            }
        }

        private void RefreshToolkitCanvas(bool rebuild = false)
        {
            if (toolkitCanvas == null)
            {
                return;
            }

            if (rebuild)
            {
                toolkitCanvas.RequestRebuild();
            }
            else
            {
                toolkitCanvas.RefreshGraphAppearance();
            }

            toolkitControls?.MarkDirtyRepaint();
            rootVisualElement.style.backgroundColor = WindowBackgroundColor;
        }

        private void DrawEmptyState()
        {
            Rect contentRect = new Rect(12f, 12f, position.width - 24f, 52f);
            EditorGUI.DrawRect(contentRect, PanelBackgroundColor);
            GUI.Box(contentRect, GUIContent.none, HelpBoxStyle);
            EditorGUI.LabelField(
                new Rect(contentRect.x + 10f, contentRect.y + 10f, contentRect.width - 20f, 32f),
                "Create or load a dialog graph.");
        }

        private void DrawControlsOverlay()
        {
            const float toggleButtonWidth = 24f;
            const float toggleButtonHeight = 64f;
            const float spacing = 6f;
            const float padding = 10f;
            const float collapsedToggleLeftOffset = 6f;
            float panelHeight = Mathf.Max(120f, position.height);
            float panelY = position.height - panelHeight;
            float panelX = 0f;
            Rect panelRect = new Rect(panelX, panelY, OverlayPanelWidth, panelHeight);

            float toggleX = isControlsPanelExpanded
                ? panelRect.xMax - toggleButtonWidth * 0.5f
                : collapsedToggleLeftOffset;
            float toggleY = panelRect.y + panelRect.height * 0.5f - toggleButtonHeight * 0.5f;
            Rect toggleRect = new Rect(toggleX, toggleY, toggleButtonWidth, toggleButtonHeight);

            if (!isControlsPanelExpanded)
            {
                if (DrawButton(toggleRect, ">"))
                {
                    isControlsPanelExpanded = true;
                }

                return;
            }

            const float buttonHeight = 28f;
            float y = padding;

            EditorGUI.DrawRect(panelRect, PanelBackgroundColor);
            GUI.Box(panelRect, GUIContent.none, HelpBoxStyle);
            GUILayout.BeginArea(panelRect, GUIContent.none, HelpBoxStyle);
            float contentWidth = OverlayPanelWidth - padding * 2f;

            EditorGUI.LabelField(new Rect(padding, padding, contentWidth, 18f), "Dialogs Folder Path:");
            y += 18f;

            dialogsFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), dialogsFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Dialogs", ref dialogsFolderPath, DialogsPathKey);
            }

            if (DrawButton(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                EditorPrefs.SetString(DialogsPathKey, dialogsFolderPath);
            }

            y += 28f;

            EditorGUI.LabelField(new Rect(padding, y, contentWidth, 18f), "Phrases Folder Path:");
            y += 18f;

            phrasesFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), phrasesFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Dialog Phrases", ref phrasesFolderPath, PhrasesPathKey);
            }

            if (DrawButton(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                EditorPrefs.SetString(PhrasesPathKey, phrasesFolderPath);
            }

            y += 36f;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), GetThemeToggleLabel()))
            {
                useLightTheme = !useLightTheme;
                EditorPrefs.SetBool(ThemeKey, useLightTheme);
                RefreshToolkitCanvas();
                Repaint();
            }

            y += buttonHeight + spacing;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "New Dialog"))
            {
                CreateNewGraph();
            }

            y += buttonHeight + spacing;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Load Dialog"))
            {
                LoadGraph();
            }

            y += buttonHeight + spacing;

            EditorGUI.BeginDisabledGroup(currentGraph == null);
            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "New Phrase"))
            {
                CreateNewPhrase();
            }
            EditorGUI.EndDisabledGroup();

            y += buttonHeight + spacing;

            if (currentGraph != null)
            {
                if (currentGraph.EntryPhrase == null)
                {
                    EditorGUI.HelpBox(
                        new Rect(padding, y, contentWidth, 40f),
                        "Entry phrase is not selected. The dialog will not work without it.",
                        MessageType.Warning);
                    y += 46f;
                }

                EditorGUI.BeginDisabledGroup(currentGraph.EntryPhrase == null);
                if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Ping Entry Phrase"))
                {
                    EditorGUIUtility.PingObject(currentGraph.EntryPhrase);
                    Selection.activeObject = currentGraph.EntryPhrase;
                }

                EditorGUI.EndDisabledGroup();
                y += buttonHeight + spacing;
            }

            if (isSelectingTargetPhrase)
            {
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = DangerButtonColor;
                if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Cancel Selection"))
                {
                    CancelTargetSelection();
                }

                GUI.backgroundColor = previousColor;
            }

            GUILayout.EndArea();

            if (DrawButton(toggleRect, "<"))
            {
                isControlsPanelExpanded = false;
            }
        }

        private void PickFolder(string title, ref string folderPath, string prefsKey)
        {
            string selected = EditorUtility.OpenFolderPanel(title, "Assets", "");
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            if (!selected.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Folder",
                    "Please select a folder inside your Assets directory.",
                    "OK");
                return;
            }

            folderPath = "Assets" + selected.Substring(Application.dataPath.Length);
            EditorPrefs.SetString(prefsKey, folderPath);
        }

        private void CreateNewGraph()
        {
            if (!EnsureFolderExists(dialogsFolderPath, "Please specify the folder for saving dialogs."))
            {
                return;
            }

            string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dialogsFolderPath, "DialogGraph.asset"));
            currentGraph = CreateInstance<DialogGraph>();
            currentGraph.name = Path.GetFileNameWithoutExtension(targetPath);

            AssetDatabase.CreateAsset(currentGraph, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            InvalidateGraphStructure();

            EditorGUIUtility.PingObject(currentGraph);
            Selection.activeObject = currentGraph;
        }

        private void LoadGraph()
        {
            string path = EditorUtility.OpenFilePanel("Load Dialog Graph", "Assets", "asset");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = "Assets" + path.Replace(Application.dataPath, "");
            currentGraph = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog("Invalid Asset", "Selected asset is not a DialogGraph.", "OK");
                return;
            }

            InvalidateGraphStructure();
        }

        private void CreateNewPhrase()
        {
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog(
                    "No Dialog Selected",
                    "Please create or load a dialog first.",
                    "OK");
                return;
            }

            if (!EnsureFolderExists(phrasesFolderPath, "Please specify the folder for saving phrases."))
            {
                return;
            }

            string fileName = $"DialogPhrase_{currentGraph.Nodes.Count}.asset";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(phrasesFolderPath, fileName));

            var phrase = CreateInstance<DialogPhrase>();
            phrase.name = Path.GetFileNameWithoutExtension(targetPath);

            AssetDatabase.CreateAsset(phrase, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var newNode = new DialogNode(phrase)
            {
                Position = GetCenteredNodePosition(new Vector2(DialogNodeWidth, 220f))
            };

            currentGraph.Nodes.Add(newNode);
            MarkDirty(currentGraph);
            InvalidateGraphStructure();

            EditorGUIUtility.PingObject(phrase);
            Selection.activeObject = phrase;
        }

        private bool EnsureFolderExists(string folderPath, string emptyPathMessage)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                EditorUtility.DisplayDialog("Path not set", emptyPathMessage, "OK");
                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            return true;
        }

        private Vector2 GetCenteredNodePosition(Vector2 nodeSize)
        {
            Vector2 screenCenter = new Vector2(position.width / 2f, position.height / 2f);
            Vector2 graphCenter = (screenCenter - panOffset) / zoom;
            graphCenter.y += 120f;
            return graphCenter - nodeSize * 0.5f;
        }

        private void DrawGraphArea()
        {
            Event currentEvent = Event.current;
            if (graphStructureDirty)
            {
                CleanupGraph();
                graphStructureDirty = false;
            }

            if (graphCachesDirty)
            {
                RebuildGraphCaches();
            }

            SynchronizeNodeRects();
            answerAnchorPositions.Clear();
            HandleZoom(currentEvent);
            HandlePan(currentEvent);
            Rect visibleGraphRect = GraphEditorCanvasUtility.GetVisibleGraphRect(position, panOffset, zoom);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            GUI.EndClip();
            GUI.EndClip();
            GUI.BeginClip(new Rect(Vector2.zero, new Vector2(WorkspaceWidth, WorkspaceHeight)));
            GUI.BeginClip(new Rect(Vector2.zero, new Vector2(WorkspaceWidth, WorkspaceHeight)));

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(panOffset, Quaternion.identity, Vector3.one * zoom);

            if (currentEvent.type == EventType.Repaint)
            {
                GraphEditorCanvasUtility.DrawBackgroundGrid(
                    visibleGraphRect,
                    CanvasBackgroundColor,
                    MinorGridColor,
                    MajorGridColor);
            }

            BeginWindows();

            for (int i = 0; i < currentGraph.Nodes.Count; i++)
            {
                DialogNode node = currentGraph.Nodes[i];
                if (node == null || !nodeRects.TryGetValue(node, out Rect rect))
                {
                    continue;
                }

                if (!GraphEditorCanvasUtility.IsAtLeastPartiallyVisible(rect, visibleGraphRect))
                {
                    continue;
                }

                Color previousColor = GUI.color;
                GUI.color = GetNodeTint(node);
                Rect previousRect = rect;
                bool shouldRecalculateLayout = phrasesWithDirtyLayout.Contains(node.Phrase);
                if (shouldRecalculateLayout)
                {
                    rect.height = 0f;
                }

                rect = GUILayout.Window(i, rect, _ => DrawNodeWindow(node), GetNodeTitle(node), NodeWindowStyle);
                GUI.color = previousColor;

                nodeRects[node] = rect;
                node.Position = rect.position;
                if (!RectApproximatelyEqual(previousRect, rect))
                {
                    InvalidateConnectionRouteCache();
                }

                // Keep the reset through the subsequent Repaint: Layout calculates the natural
                // size and Repaint returns it, including when a node has just become smaller.
                if (shouldRecalculateLayout && currentEvent.type == EventType.Layout)
                {
                    phrasesAwaitingRepaintAfterLayout.Add(node.Phrase);
                }
                else if (shouldRecalculateLayout && currentEvent.type == EventType.Repaint &&
                         phrasesAwaitingRepaintAfterLayout.Remove(node.Phrase))
                {
                    phrasesWithDirtyLayout.Remove(node.Phrase);
                }
            }

            EndWindows();
            HandleConnectionHighlightSelection(currentEvent);

            if (currentEvent.type == EventType.Repaint)
            {
                DrawNodeMarkers(visibleGraphRect);
                DrawConnections();
            }

            DrawTargetSelectionOverlay(visibleGraphRect);

            GUI.matrix = oldMatrix;
            EditorGUILayout.EndScrollView();
        }

        private void SynchronizeNodeRects()
        {
            graphNodeSet.Clear();
            staleNodeRects.Clear();
            bool layoutChanged = false;

            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node == null)
                {
                    continue;
                }

                graphNodeSet.Add(node);
                if (!nodeRects.TryGetValue(node, out Rect rect))
                {
                    nodeRects[node] = new Rect(node.Position, new Vector2(DialogNodeWidth, 220f));
                    layoutChanged = true;
                    continue;
                }

                if (!ApproximatelyEqual(rect.position, node.Position))
                {
                    rect.position = node.Position;
                    nodeRects[node] = rect;
                    layoutChanged = true;
                }
            }

            foreach (DialogNode node in nodeRects.Keys)
            {
                if (!graphNodeSet.Contains(node))
                {
                    staleNodeRects.Add(node);
                }
            }

            foreach (DialogNode node in staleNodeRects)
            {
                nodeRects.Remove(node);
                layoutChanged = true;
            }

            if (layoutChanged)
            {
                InvalidateConnectionRouteCache();
            }
        }

        private void CleanupGraph()
        {
            if (currentGraph == null)
            {
                return;
            }

            bool graphChanged = false;

            for (int i = currentGraph.Nodes.Count - 1; i >= 0; i--)
            {
                DialogNode node = currentGraph.Nodes[i];
                if (node == null)
                {
                    currentGraph.Nodes.RemoveAt(i);
                    graphChanged = true;
                    continue;
                }

                if (node.Phrase == null || !AssetDatabase.Contains(node.Phrase))
                {
                    if (currentGraph.IsEntryPhrase(node.Phrase))
                    {
                        currentGraph.SetEntryPhrase(null);
                    }

                    currentGraph.Nodes.RemoveAt(i);
                    graphChanged = true;
                }
            }

            if (graphChanged)
            {
                MarkDirty(currentGraph);
                InvalidateGraphCaches();
            }
        }

        private void RebuildGraphCaches()
        {
            if (!graphCachesDirty)
            {
                return;
            }

            phraseToNodeLookup.Clear();
            orphanPhrases.Clear();

            if (currentGraph == null || currentGraph.Nodes == null)
            {
                return;
            }

            var reachablePhrases = new HashSet<DialogPhrase>();
            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node?.Phrase == null)
                {
                    continue;
                }

                phraseToNodeLookup[node.Phrase] = node;
                foreach (DialogAnswer answer in node.Phrase.Answers)
                {
                    if (answer?.NextPhrase != null)
                    {
                        reachablePhrases.Add(answer.NextPhrase);
                    }
                }
            }

            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node?.Phrase == null || currentGraph.IsEntryPhrase(node.Phrase) || node.Phrase.IsQuestPhrase)
                {
                    continue;
                }

                if (!reachablePhrases.Contains(node.Phrase))
                {
                    orphanPhrases.Add(node.Phrase);
                }
            }

            graphCachesDirty = false;
            InvalidateConnectionRouteCache();
        }

        private void InvalidateGraphCaches()
        {
            phraseToNodeLookup.Clear();
            orphanPhrases.Clear();
            phraseDisplayNameCache.Clear();
            restoreExitAbilityCache.Clear();
            phrasesWithDirtyLayout.Clear();
            phrasesAwaitingRepaintAfterLayout.Clear();
            graphCachesDirty = true;
            InvalidateConnectionRouteCache();
            RefreshToolkitCanvas();
        }

        private void InvalidateGraphStructure()
        {
            graphStructureDirty = true;
            InvalidateGraphCaches();
            RefreshToolkitCanvas(true);
        }

        private void InvalidateConnectionRouteCache()
        {
            connectionRouteCache.Clear();
            connectionLayoutVersion++;
        }

        private static void InvalidateStaticEditorCaches()
        {
            cachedStringTableCollections = null;
            cachedStringTableOptions = null;
            localizedEntryOptionsCache.Clear();
            cachedQuestGraphs = null;
            cachedQuestSourceNodes = null;
            cachedTerminalQuestNodes = null;
        }

        private void HandleProjectChanged()
        {
            InvalidateGraphStructure();
            InvalidateStaticEditorCaches();
            RefreshToolkitCanvas(true);
            Repaint();
        }

        private void HandleZoom(Event currentEvent)
        {
            if (currentEvent.type != EventType.ScrollWheel)
            {
                return;
            }

            float zoomDelta = -currentEvent.delta.y * 0.05f;
            float oldZoom = zoom;
            zoom = Mathf.Clamp(zoom + zoomDelta, ZoomMin, ZoomMax);

            Vector2 windowCenter = new Vector2(position.width / 2f, position.height / 2f);
            panOffset = (panOffset - windowCenter) * (zoom / oldZoom) + windowCenter;

            ClampPanToWorkspace(WorkspaceWidth, WorkspaceHeight);
            currentEvent.Use();
        }

        private void HandlePan(Event currentEvent)
        {
            if (currentEvent.type != EventType.MouseDrag || currentEvent.button != 1)
            {
                return;
            }

            panOffset += currentEvent.delta;
            ClampPanToWorkspace(WorkspaceWidth, WorkspaceHeight);
            currentEvent.Use();
            Repaint();
        }

        private void DrawNodeMarkers(Rect visibleGraphRect)
        {
            foreach (KeyValuePair<DialogNode, Rect> pair in nodeRects)
            {
                DialogNode node = pair.Key;
                if (node.Phrase == null || !GraphEditorCanvasUtility.IsAtLeastPartiallyVisible(pair.Value, visibleGraphRect))
                {
                    continue;
                }

                Rect badgeRect = new Rect(pair.Value.x + 6f, pair.Value.y + 6f, 18f, 18f);
                Color previous = GUI.backgroundColor;

                if (currentGraph.IsEntryPhrase(node.Phrase))
                {
                    GUI.backgroundColor = StartBadgeColor;
                    GUI.Box(badgeRect, "S");
                }
                else if (IsOrphanPhrase(node.Phrase))
                {
                    GUI.backgroundColor = WarningBadgeColor;
                    GUI.Box(badgeRect, "!");
                }

                GUI.backgroundColor = previous;
            }
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();

            foreach (KeyValuePair<DialogNode, Rect> pair in nodeRects)
            {
                DialogNode node = pair.Key;
                DialogPhrase phrase = node.Phrase;
                if (phrase == null)
                {
                    continue;
                }

                foreach (DialogAnswer answer in phrase.Answers)
                {
                    if (answer == null || answer.NextPhrase == null)
                    {
                        continue;
                    }

                    if (!phraseToNodeLookup.TryGetValue(answer.NextPhrase, out DialogNode targetNode))
                    {
                        continue;
                    }

                    if (!nodeRects.TryGetValue(node, out Rect sourceRect) ||
                        !nodeRects.TryGetValue(targetNode, out Rect targetRect))
                    {
                        continue;
                    }

                    Vector2 startPos;
                    if (!answerAnchorPositions.TryGetValue(answer, out startPos))
                    {
                        startPos = new Vector2(sourceRect.xMax - 12f, sourceRect.center.y);
                    }

                    Handles.color = GetConnectionColor(node, targetNode);
                    Vector2 endPos = GetNearestSideCenter(targetRect, startPos);
                    (Vector2 startTangent, Vector2 endTangent) = GetOrBuildConnectionTangents(
                        answer,
                        startPos,
                        endPos,
                        sourceRect,
                        targetRect,
                        node,
                        targetNode);

                    Handles.DrawBezier(startPos, endPos, startTangent, endTangent, Handles.color, null, 3f);
                    DrawConnectionArrow(endPos, endPos - endTangent);
                }
            }

            Handles.EndGUI();
        }

        private void HandleConnectionHighlightSelection(Event currentEvent)
        {
            if (isSelectingTargetPhrase ||
                currentEvent.rawType != EventType.MouseDown ||
                currentEvent.button != 0)
            {
                return;
            }

            Vector2 graphMousePosition = GetGraphMousePosition(currentEvent.mousePosition);
            bool clickedNode = nodeRects.Any(pair => pair.Value.Contains(graphMousePosition));
            if (!clickedNode && activeConnectionNode != null)
            {
                activeConnectionNode = null;
                Repaint();
            }
        }

        private Color GetConnectionColor(DialogNode sourceNode, DialogNode targetNode)
        {
            if (activeConnectionNode == null)
            {
                return PrimaryConnectionColor;
            }

            if (sourceNode == activeConnectionNode)
            {
                return SourceHighlightConnectionColor;
            }

            if (targetNode == activeConnectionNode)
            {
                return TargetHighlightConnectionColor;
            }

            return PrimaryConnectionColor;
        }

        private (Vector2 StartTangent, Vector2 EndTangent) GetOrBuildConnectionTangents(
            DialogAnswer answer,
            Vector2 startPos,
            Vector2 endPos,
            Rect sourceRect,
            Rect targetRect,
            DialogNode sourceNode,
            DialogNode targetNode)
        {
            if (answer != null &&
                connectionRouteCache.TryGetValue(answer, out CachedConnectionRoute cachedRoute) &&
                cachedRoute.LayoutVersion == connectionLayoutVersion &&
                ApproximatelyEqual(cachedRoute.StartPos, startPos) &&
                RectApproximatelyEqual(cachedRoute.SourceRect, sourceRect) &&
                RectApproximatelyEqual(cachedRoute.TargetRect, targetRect))
            {
                return (cachedRoute.StartTangent, cachedRoute.EndTangent);
            }

            connectionObstacleRects.Clear();
            foreach (KeyValuePair<DialogNode, Rect> pair in nodeRects)
            {
                if (pair.Key != sourceNode && pair.Key != targetNode)
                {
                    connectionObstacleRects.Add(ExpandRect(pair.Value, 8f));
                }
            }

            (Vector2 startTangent, Vector2 endTangent) = ResolveConnectionTangents(
                startPos,
                endPos,
                sourceRect,
                targetRect,
                connectionObstacleRects);
            if (answer != null)
            {
                connectionRouteCache[answer] = new CachedConnectionRoute(
                    connectionLayoutVersion,
                    startPos,
                    sourceRect,
                    targetRect,
                    startTangent,
                    endTangent);
            }

            return (startTangent, endTangent);
        }

        private static Vector2[] BuildConnectionRoute(Vector2 startPos, Rect sourceRect, Rect targetRect, IReadOnlyList<Rect> expandedNodeRects)
        {
            const float clearance = 24f;

            ConnectionPort startPort = GetSourcePort(startPos, sourceRect, targetRect, clearance);
            ConnectionPort endPort = GetTargetPort(sourceRect, targetRect, clearance);

            return SimplifyRoute(new[]
            {
                startPos,
                startPort.OuterPoint,
                endPort.OuterPoint,
                endPort.EdgePoint
            });
        }

        private static Vector2[] BuildSoftDetourRoute(
            Vector2 startPos,
            ConnectionPort startPort,
            ConnectionPort endPort,
            IReadOnlyList<Rect> obstacles)
        {
            const float detourPadding = 18f;

            List<Rect> blockingRects = obstacles
                .Where(rect => DoesStraightSegmentIntersectRect(startPort.OuterPoint, endPort.OuterPoint, rect))
                .ToList();

            if (blockingRects.Count == 0)
            {
                return null;
            }

            float minX = blockingRects.Min(rect => rect.xMin) - detourPadding;
            float maxX = blockingRects.Max(rect => rect.xMax) + detourPadding;
            float minY = blockingRects.Min(rect => rect.yMin) - detourPadding;
            float maxY = blockingRects.Max(rect => rect.yMax) + detourPadding;
            float startX = startPort.OuterPoint.x;
            float endX = endPort.OuterPoint.x;
            float startY = startPort.OuterPoint.y;
            float endY = endPort.OuterPoint.y;

            var candidates = new List<Vector2[]>
            {
                BuildCandidateRoute(
                    startPos,
                    startPort,
                    endPort,
                    new Vector2(Mathf.Lerp(startX, endX, 0.3f), minY),
                    new Vector2(Mathf.Lerp(startX, endX, 0.7f), minY),
                    obstacles),
                BuildCandidateRoute(
                    startPos,
                    startPort,
                    endPort,
                    new Vector2(Mathf.Lerp(startX, endX, 0.3f), maxY),
                    new Vector2(Mathf.Lerp(startX, endX, 0.7f), maxY),
                    obstacles),
                BuildCandidateRoute(
                    startPos,
                    startPort,
                    endPort,
                    new Vector2(minX, Mathf.Lerp(startY, endY, 0.3f)),
                    new Vector2(minX, Mathf.Lerp(startY, endY, 0.7f)),
                    obstacles),
                BuildCandidateRoute(
                    startPos,
                    startPort,
                    endPort,
                    new Vector2(maxX, Mathf.Lerp(startY, endY, 0.3f)),
                    new Vector2(maxX, Mathf.Lerp(startY, endY, 0.7f)),
                    obstacles)
            };

            return candidates
                .Where(route => route != null)
                .OrderBy(ScoreConnectionRoute)
                .FirstOrDefault();
        }

        private static Vector2[] BuildCandidateRoute(
            Vector2 startPos,
            ConnectionPort startPort,
            ConnectionPort endPort,
            Vector2 viaA,
            Vector2 viaB,
            IReadOnlyList<Rect> obstacles)
        {
            Vector2[] route = SimplifyRoute(new[]
            {
                startPos,
                startPort.OuterPoint,
                viaA,
                viaB,
                endPort.OuterPoint,
                endPort.EdgePoint
            });

            for (int i = 1; i < route.Length - 2; i++)
            {
                if (IsStraightSegmentBlocked(route[i], route[i + 1], obstacles))
                {
                    return null;
                }
            }

            return route;
        }

        private static float ScoreConnectionRoute(IReadOnlyList<Vector2> route)
        {
            if (route == null || route.Count < 2)
            {
                return float.PositiveInfinity;
            }

            float length = 0f;
            for (int i = 0; i < route.Count - 1; i++)
            {
                length += Vector2.Distance(route[i], route[i + 1]);
            }

            float turnPenalty = Mathf.Max(0, route.Count - 4) * 18f;
            return length + turnPenalty;
        }

        private static void DrawConnectionArrow(Vector2 tipPosition, Vector2 direction)
        {
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 right = new Vector2(-normalizedDirection.y, normalizedDirection.x);
            Vector2 arrowBase = tipPosition - normalizedDirection * 18f;
            Vector3[] arrow =
            {
                tipPosition,
                arrowBase + right * 7.5f,
                arrowBase - right * 7.5f
            };
            Handles.DrawAAConvexPolygon(arrow);
        }

        private static (Vector2 StartTangent, Vector2 EndTangent) ResolveConnectionTangents(
            Vector2 startPos,
            Vector2 endPos,
            Rect sourceRect,
            Rect targetRect,
            IReadOnlyList<Rect> obstacles)
        {
            Vector2 endDirection = GetConnectionDirectionForRectPoint(targetRect, endPos);
            Vector2 defaultStartTangent = startPos + Vector2.right * 60f;
            Vector2 defaultEndTangent = endPos + endDirection * 60f;

            if (!IsBezierBlocked(startPos, defaultStartTangent, defaultEndTangent, endPos, obstacles))
            {
                return (defaultStartTangent, defaultEndTangent);
            }

            List<Rect> blockingRects = obstacles
                .Where(rect =>
                    DoesBezierIntersectRect(startPos, defaultStartTangent, defaultEndTangent, endPos, rect) ||
                    DoesStraightSegmentIntersectRect(startPos, endPos, rect))
                .ToList();

            if (blockingRects.Count == 0)
            {
                return (defaultStartTangent, defaultEndTangent);
            }

            float routeDistance = Vector2.Distance(startPos, endPos);
            float extendedMarginA = Mathf.Max(140f, routeDistance * 0.25f);
            float extendedMarginB = Mathf.Max(220f, routeDistance * 0.45f);
            float minX = blockingRects.Min(rect => rect.xMin) - 36f;
            float maxX = blockingRects.Max(rect => rect.xMax) + 36f;
            float minY = blockingRects.Min(rect => rect.yMin) - 36f;
            float maxY = blockingRects.Max(rect => rect.yMax) + 36f;
            float middleX = Mathf.Lerp(startPos.x, endPos.x, 0.5f);
            float middleY = Mathf.Lerp(startPos.y, endPos.y, 0.5f);
            var laneXs = new List<float> { minX, maxX };
            var laneYs = new List<float> { minY, maxY };
            float[] laneMargins = { 28f, 52f, 80f, 112f, extendedMarginA, extendedMarginB };

            foreach (Rect rect in blockingRects)
            {
                foreach (float margin in laneMargins)
                {
                    AddApproximatelyUnique(laneXs, rect.xMin - margin);
                    AddApproximatelyUnique(laneXs, rect.xMax + margin);
                    AddApproximatelyUnique(laneYs, rect.yMin - margin);
                    AddApproximatelyUnique(laneYs, rect.yMax + margin);
                }
            }

            AddApproximatelyUnique(laneXs, minX - extendedMarginA);
            AddApproximatelyUnique(laneXs, maxX + extendedMarginA);
            AddApproximatelyUnique(laneXs, minX - extendedMarginB);
            AddApproximatelyUnique(laneXs, maxX + extendedMarginB);
            AddApproximatelyUnique(laneYs, minY - extendedMarginA);
            AddApproximatelyUnique(laneYs, maxY + extendedMarginA);
            AddApproximatelyUnique(laneYs, minY - extendedMarginB);
            AddApproximatelyUnique(laneYs, maxY + extendedMarginB);

            var candidates = new List<(Vector2 StartTangent, Vector2 EndTangent)>();
            float nearStartX = Mathf.Lerp(startPos.x, endPos.x, 0.12f);
            float farEndX = Mathf.Lerp(startPos.x, endPos.x, 0.88f);
            float nearStartY = Mathf.Lerp(startPos.y, endPos.y, 0.12f);
            float farEndY = Mathf.Lerp(startPos.y, endPos.y, 0.88f);

            foreach (float laneY in laneYs)
            {
                AddBezierCandidate(candidates, new Vector2(Mathf.Lerp(startPos.x, endPos.x, 0.30f), laneY), new Vector2(Mathf.Lerp(startPos.x, endPos.x, 0.70f), laneY));
                AddBezierCandidate(candidates, new Vector2(middleX, laneY), new Vector2(middleX, laneY));
                AddBezierCandidate(candidates, new Vector2(Mathf.Lerp(startPos.x, endPos.x, 0.20f), laneY), new Vector2(Mathf.Lerp(startPos.x, endPos.x, 0.80f), laneY));
                AddBezierCandidate(candidates, new Vector2(nearStartX, laneY), new Vector2(farEndX, laneY));
            }

            foreach (float laneX in laneXs)
            {
                AddBezierCandidate(candidates, new Vector2(laneX, Mathf.Lerp(startPos.y, endPos.y, 0.30f)), new Vector2(laneX, Mathf.Lerp(startPos.y, endPos.y, 0.70f)));
                AddBezierCandidate(candidates, new Vector2(laneX, middleY), new Vector2(laneX, middleY));
                AddBezierCandidate(candidates, new Vector2(laneX, Mathf.Lerp(startPos.y, endPos.y, 0.20f)), new Vector2(laneX, Mathf.Lerp(startPos.y, endPos.y, 0.80f)));
                AddBezierCandidate(candidates, new Vector2(laneX, nearStartY), new Vector2(laneX, farEndY));
            }

            (Vector2 StartTangent, Vector2 EndTangent) bestCandidate = (defaultStartTangent, defaultEndTangent);
            int bestIntersectionCount = CountBezierIntersections(startPos, defaultStartTangent, defaultEndTangent, endPos, obstacles);
            float bestScore = ScoreBezierCandidate(startPos, defaultStartTangent, defaultEndTangent, endPos);

            foreach ((Vector2 StartTangent, Vector2 EndTangent) candidate in candidates)
            {
                int intersectionCount = CountBezierIntersections(startPos, candidate.StartTangent, candidate.EndTangent, endPos, obstacles);
                float candidateScore = ScoreBezierCandidate(startPos, candidate.StartTangent, candidate.EndTangent, endPos);
                if (intersectionCount < bestIntersectionCount ||
                    intersectionCount == bestIntersectionCount && candidateScore < bestScore)
                {
                    bestIntersectionCount = intersectionCount;
                    bestScore = candidateScore;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        private static float ScoreBezierCandidate(Vector2 startPos, Vector2 startTangent, Vector2 endTangent, Vector2 endPos)
        {
            return Vector2.Distance(startPos, startTangent) +
                   Vector2.Distance(startTangent, endTangent) +
                   Vector2.Distance(endTangent, endPos);
        }

        private static void AddBezierCandidate(
            ICollection<(Vector2 StartTangent, Vector2 EndTangent)> candidates,
            Vector2 startTangent,
            Vector2 endTangent)
        {
            foreach ((Vector2 StartTangent, Vector2 EndTangent) existing in candidates)
            {
                if (ApproximatelyEqual(existing.StartTangent, startTangent) &&
                    ApproximatelyEqual(existing.EndTangent, endTangent))
                {
                    return;
                }
            }

            candidates.Add((startTangent, endTangent));
        }

        private static void AddApproximatelyUnique(ICollection<float> values, float value)
        {
            foreach (float existing in values)
            {
                if (Mathf.Abs(existing - value) < 0.5f)
                {
                    return;
                }
            }

            values.Add(value);
        }

        private static bool IsBezierBlocked(
            Vector2 startPos,
            Vector2 startTangent,
            Vector2 endTangent,
            Vector2 endPos,
            IReadOnlyList<Rect> obstacles)
        {
            return CountBezierIntersections(startPos, startTangent, endTangent, endPos, obstacles) > 0;
        }

        private static int CountBezierIntersections(
            Vector2 startPos,
            Vector2 startTangent,
            Vector2 endTangent,
            Vector2 endPos,
            IReadOnlyList<Rect> obstacles)
        {
            int intersections = 0;
            foreach (Rect obstacle in obstacles)
            {
                if (DoesBezierIntersectRect(startPos, startTangent, endTangent, endPos, obstacle))
                {
                    intersections++;
                }
            }

            return intersections;
        }

        private static bool DoesBezierIntersectRect(
            Vector2 startPos,
            Vector2 startTangent,
            Vector2 endTangent,
            Vector2 endPos,
            Rect rect)
        {
            const int samples = 40;
            const float inset = 0.5f;

            Rect innerRect = Rect.MinMaxRect(
                rect.xMin + inset,
                rect.yMin + inset,
                rect.xMax - inset,
                rect.yMax - inset);

            Vector2 previousPoint = startPos;
            for (int i = 1; i < samples; i++)
            {
                float t = i / (float)samples;
                Vector2 point = EvaluateBezierPoint(startPos, startTangent, endTangent, endPos, t);
                if (innerRect.Contains(point) || DoesStraightSegmentIntersectRect(previousPoint, point, innerRect))
                {
                    return true;
                }

                previousPoint = point;
            }

            return DoesStraightSegmentIntersectRect(previousPoint, endPos, innerRect);
        }

        private static Vector2 EvaluateBezierPoint(
            Vector2 startPos,
            Vector2 startTangent,
            Vector2 endTangent,
            Vector2 endPos,
            float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * startPos +
                   3f * oneMinusT * oneMinusT * t * startTangent +
                   3f * oneMinusT * t * t * endTangent +
                   t * t * t * endPos;
        }

        private static Vector2 GetNearestSideCenter(Rect rect, Vector2 point)
        {
            float leftDistance = Mathf.Abs(point.x - rect.xMin);
            float rightDistance = Mathf.Abs(point.x - rect.xMax);
            float topDistance = Mathf.Abs(point.y - rect.yMin);
            float bottomDistance = Mathf.Abs(point.y - rect.yMax);

            float minHorizontal = Mathf.Min(leftDistance, rightDistance);
            float minVertical = Mathf.Min(topDistance, bottomDistance);

            if (minHorizontal < minVertical)
            {
                return leftDistance <= rightDistance
                    ? new Vector2(rect.xMin, rect.center.y)
                    : new Vector2(rect.xMax, rect.center.y);
            }

            return topDistance <= bottomDistance
                ? new Vector2(rect.center.x, rect.yMin)
                : new Vector2(rect.center.x, rect.yMax);
        }

        private static Vector2 GetConnectionDirectionForRectPoint(Rect rect, Vector2 point)
        {
            const float epsilon = 0.01f;

            if (Mathf.Abs(point.x - rect.xMin) < epsilon)
            {
                return Vector2.left;
            }

            if (Mathf.Abs(point.x - rect.xMax) < epsilon)
            {
                return Vector2.right;
            }

            if (Mathf.Abs(point.y - rect.yMin) < epsilon)
            {
                return Vector2.up;
            }

            if (Mathf.Abs(point.y - rect.yMax) < epsilon)
            {
                return Vector2.down;
            }

            Vector2 fallback = point - rect.center;
            return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector2.left;
        }

        private static void DrawSmoothedConnection(IReadOnlyList<Vector2> routePoints)
        {
            if (routePoints == null || routePoints.Count < 2)
            {
                return;
            }

            const float lineWidth = 3.5f;
            const float cornerRadius = 22f;

            if (routePoints.Count == 2)
            {
                Handles.DrawAAPolyLine(lineWidth, routePoints.Select(point => (Vector3)point).ToArray());
                return;
            }

            Vector2 currentStart = routePoints[0];

            for (int i = 1; i < routePoints.Count - 1; i++)
            {
                Vector2 corner = routePoints[i];
                Vector2 previous = routePoints[i - 1];
                Vector2 next = routePoints[i + 1];

                Vector2 incomingDirection = (corner - previous).normalized;
                Vector2 outgoingDirection = (next - corner).normalized;

                float incomingLength = Vector2.Distance(previous, corner);
                float outgoingLength = Vector2.Distance(corner, next);
                float radius = Mathf.Min(cornerRadius, incomingLength * 0.5f, outgoingLength * 0.5f);

                if (radius <= 0.01f || ApproximatelyEqual(incomingDirection, outgoingDirection))
                {
                    Handles.DrawAAPolyLine(lineWidth, new Vector3[] { currentStart, corner });
                    currentStart = corner;
                    continue;
                }

                Vector2 curveStart = corner - incomingDirection * radius;
                Vector2 curveEnd = corner + outgoingDirection * radius;

                Handles.DrawAAPolyLine(lineWidth, new Vector3[] { currentStart, curveStart });

                Handles.DrawBezier(
                    curveStart,
                    curveEnd,
                    curveStart + incomingDirection * radius,
                    curveEnd - outgoingDirection * radius,
                    Handles.color,
                    null,
                    lineWidth);

                currentStart = curveEnd;
            }

            Handles.DrawAAPolyLine(lineWidth, new Vector3[] { currentStart, routePoints[routePoints.Count - 1] });
        }

        private static List<Vector2> FindOrthogonalPath(Vector2 startPoint, Vector2 endPoint, IReadOnlyList<Rect> obstacles)
        {
            var xCoords = new List<float>();
            var yCoords = new List<float>();

            AddUniqueCoordinate(xCoords, startPoint.x);
            AddUniqueCoordinate(xCoords, endPoint.x);
            AddUniqueCoordinate(yCoords, startPoint.y);
            AddUniqueCoordinate(yCoords, endPoint.y);

            foreach (Rect obstacle in obstacles)
            {
                AddUniqueCoordinate(xCoords, obstacle.xMin);
                AddUniqueCoordinate(xCoords, obstacle.xMax);
                AddUniqueCoordinate(yCoords, obstacle.yMin);
                AddUniqueCoordinate(yCoords, obstacle.yMax);
            }

            var points = new List<Vector2>();
            var pointIndex = new Dictionary<string, int>();

            foreach (float x in xCoords)
            {
                foreach (float y in yCoords)
                {
                    Vector2 point = new Vector2(x, y);
                    if (IsPointInsideAnyRect(point, obstacles))
                    {
                        continue;
                    }

                    pointIndex[GetPointKey(point)] = points.Count;
                    points.Add(point);
                }
            }

            if (!TryGetPointIndex(pointIndex, startPoint, out int startIndex))
            {
                return null;
            }

            int endIndex = TryGetPointIndex(pointIndex, endPoint, out int resolvedEndIndex)
                ? resolvedEndIndex
                : -1;

            var adjacency = new List<int>[points.Count];
            for (int i = 0; i < adjacency.Length; i++)
            {
                adjacency[i] = new List<int>();
            }

            foreach (float y in yCoords)
            {
                List<int> row = points
                    .Select((point, index) => new { point, index })
                    .Where(item => Mathf.Approximately(item.point.y, y))
                    .OrderBy(item => item.point.x)
                    .Select(item => item.index)
                    .ToList();

                ConnectAdjacentPoints(row, points, adjacency, obstacles);
            }

            foreach (float x in xCoords)
            {
                List<int> column = points
                    .Select((point, index) => new { point, index })
                    .Where(item => Mathf.Approximately(item.point.x, x))
                    .OrderBy(item => item.point.y)
                    .Select(item => item.index)
                    .ToList();

                ConnectAdjacentPoints(column, points, adjacency, obstacles);
            }

            return FindShortestPath(points, adjacency, startIndex, endIndex, endPoint);
        }

        private static List<Vector2> FindShortestPath(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<int>[] adjacency,
            int startIndex,
            int endIndex,
            Vector2 targetPoint)
        {
            float[] distances = Enumerable.Repeat(float.PositiveInfinity, points.Count).ToArray();
            int[] previous = Enumerable.Repeat(-1, points.Count).ToArray();
            bool[] visited = new bool[points.Count];

            distances[startIndex] = 0f;
            int bestReachableIndex = startIndex;
            float bestReachableDistanceToEnd = Vector2.Distance(points[startIndex], targetPoint);

            while (true)
            {
                int current = -1;
                float bestDistance = float.PositiveInfinity;
                float bestHeuristic = float.PositiveInfinity;

                for (int i = 0; i < points.Count; i++)
                {
                    if (visited[i] || float.IsPositiveInfinity(distances[i]))
                    {
                        continue;
                    }

                    float heuristic = distances[i] + Vector2.Distance(points[i], targetPoint);
                    if (heuristic < bestHeuristic || Mathf.Approximately(heuristic, bestHeuristic) && distances[i] < bestDistance)
                    {
                        current = i;
                        bestDistance = distances[i];
                        bestHeuristic = heuristic;
                    }
                }

                if (current == -1)
                {
                    return ReconstructPath(points, previous, bestReachableIndex);
                }

                if (endIndex >= 0 && current == endIndex)
                {
                    return ReconstructPath(points, previous, endIndex);
                }

                visited[current] = true;
                float currentDistanceToEnd = Vector2.Distance(points[current], targetPoint);
                if (currentDistanceToEnd + 0.01f < bestReachableDistanceToEnd ||
                    Mathf.Approximately(currentDistanceToEnd, bestReachableDistanceToEnd) && distances[current] < distances[bestReachableIndex])
                {
                    bestReachableDistanceToEnd = currentDistanceToEnd;
                    bestReachableIndex = current;
                }

                foreach (int neighbor in adjacency[current])
                {
                    if (visited[neighbor])
                    {
                        continue;
                    }

                    float candidateDistance = distances[current] + Vector2.Distance(points[current], points[neighbor]);
                    if (candidateDistance + 0.01f < distances[neighbor])
                    {
                        distances[neighbor] = candidateDistance;
                        previous[neighbor] = current;
                    }
                }
            }

        }

        private static List<Vector2> ReconstructPath(IReadOnlyList<Vector2> points, IReadOnlyList<int> previous, int endIndex)
        {
            var path = new List<Vector2>();
            for (int node = endIndex; node != -1; node = previous[node])
            {
                path.Add(points[node]);
            }

            path.Reverse();
            return path;
        }

        private static void ConnectAdjacentPoints(
            IReadOnlyList<int> indices,
            IReadOnlyList<Vector2> points,
            IList<int>[] adjacency,
            IReadOnlyList<Rect> obstacles)
        {
            for (int i = 0; i < indices.Count - 1; i++)
            {
                int a = indices[i];
                int b = indices[i + 1];
                if (!IsOrthogonalSegmentBlocked(points[a], points[b], obstacles))
                {
                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
            }
        }

        private static ConnectionPort GetSourcePort(Vector2 startPos, Rect sourceRect, Rect targetRect, float clearance)
        {
            bool preferHorizontal = Mathf.Abs(targetRect.center.x - sourceRect.center.x) >= Mathf.Abs(targetRect.center.y - sourceRect.center.y);
            if (preferHorizontal)
            {
                if (targetRect.center.x >= sourceRect.center.x)
                {
                    return new ConnectionPort(
                        new Vector2(sourceRect.xMax, startPos.y),
                        new Vector2(sourceRect.xMax + clearance, startPos.y));
                }

                return new ConnectionPort(
                    new Vector2(sourceRect.xMin, startPos.y),
                    new Vector2(sourceRect.xMin - clearance, startPos.y));
            }

            if (targetRect.center.y >= sourceRect.center.y)
            {
                return new ConnectionPort(
                    new Vector2(startPos.x, sourceRect.yMax),
                    new Vector2(startPos.x, sourceRect.yMax + clearance));
            }

            return new ConnectionPort(
                new Vector2(startPos.x, sourceRect.yMin),
                new Vector2(startPos.x, sourceRect.yMin - clearance));
        }

        private static ConnectionPort GetTargetPort(Rect sourceRect, Rect targetRect, float clearance)
        {
            Vector2 sourceCenter = sourceRect.center;
            ConnectionPort[] candidatePorts =
            {
                new(
                    new Vector2(targetRect.xMin, targetRect.center.y),
                    new Vector2(targetRect.xMin - clearance, targetRect.center.y)),
                new(
                    new Vector2(targetRect.xMax, targetRect.center.y),
                    new Vector2(targetRect.xMax + clearance, targetRect.center.y)),
                new(
                    new Vector2(targetRect.center.x, targetRect.yMin),
                    new Vector2(targetRect.center.x, targetRect.yMin - clearance)),
                new(
                    new Vector2(targetRect.center.x, targetRect.yMax),
                    new Vector2(targetRect.center.x, targetRect.yMax + clearance))
            };

            ConnectionPort bestPort = candidatePorts[0];
            float bestDistance = Vector2.SqrMagnitude(sourceCenter - bestPort.EdgePoint);

            for (int i = 1; i < candidatePorts.Length; i++)
            {
                float distance = Vector2.SqrMagnitude(sourceCenter - candidatePorts[i].EdgePoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPort = candidatePorts[i];
                }
            }

            return bestPort;
        }

        private static Rect ExpandRect(Rect rect, float margin)
        {
            return Rect.MinMaxRect(rect.xMin - margin, rect.yMin - margin, rect.xMax + margin, rect.yMax + margin);
        }

        private static bool IsPointInsideAnyRect(Vector2 point, IReadOnlyList<Rect> rects)
        {
            const float epsilon = 0.01f;
            foreach (Rect rect in rects)
            {
                if (point.x > rect.xMin + epsilon && point.x < rect.xMax - epsilon &&
                    point.y > rect.yMin + epsilon && point.y < rect.yMax - epsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOrthogonalSegmentBlocked(Vector2 start, Vector2 end, IReadOnlyList<Rect> rects)
        {
            const float epsilon = 0.01f;
            if (!Mathf.Approximately(start.x, end.x) && !Mathf.Approximately(start.y, end.y))
            {
                return true;
            }

            foreach (Rect rect in rects)
            {
                if (Mathf.Approximately(start.y, end.y))
                {
                    float y = start.y;
                    float minX = Mathf.Min(start.x, end.x);
                    float maxX = Mathf.Max(start.x, end.x);
                    bool overlapsY = y > rect.yMin + epsilon && y < rect.yMax - epsilon;
                    bool overlapsX = maxX > rect.xMin + epsilon && minX < rect.xMax - epsilon;
                    if (overlapsY && overlapsX)
                    {
                        return true;
                    }
                }
                else
                {
                    float x = start.x;
                    float minY = Mathf.Min(start.y, end.y);
                    float maxY = Mathf.Max(start.y, end.y);
                    bool overlapsX = x > rect.xMin + epsilon && x < rect.xMax - epsilon;
                    bool overlapsY = maxY > rect.yMin + epsilon && minY < rect.yMax - epsilon;
                    if (overlapsX && overlapsY)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsStraightSegmentBlocked(Vector2 start, Vector2 end, IReadOnlyList<Rect> rects)
        {
            foreach (Rect rect in rects)
            {
                if (DoesStraightSegmentIntersectRect(start, end, rect))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DoesStraightSegmentIntersectRect(Vector2 start, Vector2 end, Rect rect)
        {
            const int samples = 24;
            const float inset = 0.5f;

            Rect innerRect = Rect.MinMaxRect(
                rect.xMin + inset,
                rect.yMin + inset,
                rect.xMax - inset,
                rect.yMax - inset);

            for (int i = 1; i < samples; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)samples);
                if (innerRect.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddUniqueCoordinate(List<float> coordinates, float value)
        {
            if (coordinates.All(existing => !Mathf.Approximately(existing, value)))
            {
                coordinates.Add(value);
            }
        }

        private static bool TryGetPointIndex(IReadOnlyDictionary<string, int> pointIndex, Vector2 point, out int index)
        {
            return pointIndex.TryGetValue(GetPointKey(point), out index);
        }

        private static string GetPointKey(Vector2 point)
        {
            return $"{point.x:F3}|{point.y:F3}";
        }

        private static Vector2[] SimplifyRoute(IEnumerable<Vector2> points)
        {
            var simplified = new List<Vector2>();

            foreach (Vector2 point in points)
            {
                if (simplified.Count == 0 || !ApproximatelyEqual(simplified[simplified.Count - 1], point))
                {
                    simplified.Add(point);
                }
            }

            int index = 1;
            while (index < simplified.Count - 1)
            {
                Vector2 previous = simplified[index - 1];
                Vector2 current = simplified[index];
                Vector2 next = simplified[index + 1];

                bool sameX = Mathf.Approximately(previous.x, current.x) && Mathf.Approximately(current.x, next.x);
                bool sameY = Mathf.Approximately(previous.y, current.y) && Mathf.Approximately(current.y, next.y);
                if (sameX || sameY)
                {
                    simplified.RemoveAt(index);
                    continue;
                }

                index++;
            }

            return simplified.ToArray();
        }

        private static bool ApproximatelyEqual(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }

        private static bool RectApproximatelyEqual(Rect a, Rect b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                   Mathf.Approximately(a.y, b.y) &&
                   Mathf.Approximately(a.width, b.width) &&
                   Mathf.Approximately(a.height, b.height);
        }

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

        private sealed class LocalizedEntrySelectorWindow : EditorWindow
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
                    ApplyEntrySelectionToObject(targetObject, keyIdPropertyPath, keyPropertyPath, entries, 0);
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
                    ApplyEntrySelectionToObject(targetObject, keyIdPropertyPath, keyPropertyPath, entries, 0);
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

                        ApplyEntrySelectionToObject(targetObject, keyIdPropertyPath, keyPropertyPath, entries, entryIndex + 1);
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
                        ApplyEntrySelectionToObject(targetObject, keyIdPropertyPath, keyPropertyPath, entries, entryIndex + 1);
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
        }

        private sealed class DialogToolkitCanvas : VisualElement
        {
            private const float NodeHeaderHeight = 24f;

            private readonly DialogEditorWindow owner;
            private readonly VisualElement graphContent;
            private readonly Label emptyState;
            private readonly Dictionary<DialogNode, DialogToolkitNodeElement> nodeElements = new();
            private readonly Dictionary<DialogAnswer, DialogToolkitConnectionElement> connectionElements = new();
            private DialogNode draggedNode;
            private bool rebuildScheduled;
            private bool isPanning;
            private int panPointerId = -1;
            private Vector2 panStartPointer;
            private Vector2 panStartOffset;

            public DialogToolkitCanvas(DialogEditorWindow owner)
            {
                this.owner = owner;
                name = "dialog-toolkit-canvas";
                style.flexGrow = 1f;
                style.overflow = Overflow.Hidden;
                style.backgroundColor = owner.CanvasBackgroundColor;

                graphContent = new VisualElement
                {
                    name = "dialog-toolkit-graph-content"
                };
                graphContent.style.position = Position.Absolute;
                graphContent.style.width = WorkspaceWidth;
                graphContent.style.height = WorkspaceHeight;
                graphContent.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
                hierarchy.Add(graphContent);

                emptyState = new Label("Create or load a dialog graph.")
                {
                    name = "dialog-toolkit-empty-state"
                };
                emptyState.style.position = Position.Absolute;
                emptyState.style.left = 18f;
                emptyState.style.top = 18f;
                emptyState.style.paddingLeft = 10f;
                emptyState.style.paddingRight = 10f;
                emptyState.style.paddingTop = 8f;
                emptyState.style.paddingBottom = 8f;
                emptyState.style.borderTopWidth = 1f;
                emptyState.style.borderBottomWidth = 1f;
                emptyState.style.borderLeftWidth = 1f;
                emptyState.style.borderRightWidth = 1f;
                hierarchy.Add(emptyState);

                RegisterCallback<PointerDownEvent>(HandlePointerDown);
                RegisterCallback<PointerMoveEvent>(HandlePointerMove);
                RegisterCallback<PointerUpEvent>(HandlePointerUp);
                RegisterCallback<PointerCaptureOutEvent>(HandlePointerCaptureOut);
                RegisterCallback<WheelEvent>(HandleWheel);
                ApplyViewTransform();
            }

            public bool IsDraggingNode(DialogNode node)
            {
                return draggedNode == node;
            }

            public float OwnerZoom => owner.zoom;
            public bool OwnerIsSelectingTarget => owner.isSelectingTargetPhrase;
            public Color OwnerPanelBackgroundColor => owner.PanelBackgroundColor;
            public Color OwnerMinorGridColor => owner.MinorGridColor;
            public Color OwnerSelectionBorderColor => owner.GetSelectionOverlayColor(false);

            public void OwnerDrawNode(DialogNode node)
            {
                owner.DrawToolkitNode(node);
            }

            public void OwnerDeleteNode(DialogNode node)
            {
                owner.DeleteNode(node, false);
            }

            public string OwnerGetNodeTitle(DialogNode node)
            {
                return owner.GetNodeTitle(node);
            }

            public Color OwnerGetNodeTint(DialogNode node)
            {
                return owner.GetNodeTint(node);
            }

            public bool OwnerTryGetNodeRect(DialogNode node, out Rect rect)
            {
                return owner.nodeRects.TryGetValue(node, out rect);
            }

            public Color OwnerGetConnectionColor(DialogNode sourceNode, DialogNode targetNode)
            {
                return owner.GetConnectionColor(sourceNode, targetNode);
            }

            public (Vector2 StartTangent, Vector2 EndTangent) OwnerGetConnectionTangents(
                DialogAnswer answer,
                Vector2 startPos,
                Vector2 endPos,
                Rect sourceRect,
                Rect targetRect,
                DialogNode sourceNode,
                DialogNode targetNode)
            {
                return owner.GetOrBuildConnectionTangents(
                    answer,
                    startPos,
                    endPos,
                    sourceRect,
                    targetRect,
                    sourceNode,
                    targetNode);
            }

            public void RebuildNow()
            {
                rebuildScheduled = false;
                graphContent.Clear();
                nodeElements.Clear();
                connectionElements.Clear();
                owner.nodeRects.Clear();

                if (owner.currentGraph == null)
                {
                    emptyState.style.display = DisplayStyle.Flex;
                    RefreshGraphAppearance();
                    return;
                }

                if (owner.graphStructureDirty)
                {
                    owner.CleanupGraph();
                    owner.graphStructureDirty = false;
                }

                if (owner.graphCachesDirty)
                {
                    owner.RebuildGraphCaches();
                }

                emptyState.style.display = DisplayStyle.None;
                graphContent.Add(new DialogToolkitGridElement(owner));

                foreach (DialogNode node in owner.currentGraph.Nodes)
                {
                    if (node == null)
                    {
                        continue;
                    }

                    var nodeElement = new DialogToolkitNodeElement(this, node);
                    nodeElements[node] = nodeElement;
                    graphContent.Add(nodeElement);
                    owner.nodeRects[node] = new Rect(node.Position, new Vector2(DialogNodeWidth, 220f));
                }

                foreach (DialogNode sourceNode in owner.currentGraph.Nodes)
                {
                    if (sourceNode?.Phrase == null)
                    {
                        continue;
                    }

                    foreach (DialogAnswer answer in sourceNode.Phrase.Answers)
                    {
                        if (answer?.NextPhrase == null ||
                            !owner.phraseToNodeLookup.TryGetValue(answer.NextPhrase, out DialogNode targetNode))
                        {
                            continue;
                        }

                        var connectionElement = new DialogToolkitConnectionElement(this, sourceNode, targetNode, answer);
                        connectionElements[answer] = connectionElement;
                        graphContent.Insert(1, connectionElement);
                    }
                }

                ApplyViewTransform();
                RefreshGraphAppearance();
            }

            public void RequestRebuild()
            {
                if (rebuildScheduled)
                {
                    return;
                }

                rebuildScheduled = true;
                schedule.Execute(RebuildNow).ExecuteLater(0);
            }

            public void RefreshGraphAppearance()
            {
                style.backgroundColor = owner.CanvasBackgroundColor;
                emptyState.style.backgroundColor = owner.PanelBackgroundColor;
                emptyState.style.color = owner.ControlContentColor;
                emptyState.style.borderTopColor = owner.MinorGridColor;
                emptyState.style.borderBottomColor = owner.MinorGridColor;
                emptyState.style.borderLeftColor = owner.MinorGridColor;
                emptyState.style.borderRightColor = owner.MinorGridColor;

                foreach (DialogToolkitNodeElement nodeElement in nodeElements.Values)
                {
                    nodeElement.RefreshAppearance();
                }

                RefreshConnections();
                RefreshTargetSelection();
            }

            public void RefreshTargetSelection()
            {
                foreach (DialogToolkitNodeElement nodeElement in nodeElements.Values)
                {
                    nodeElement.RefreshTargetSelection();
                }
            }

            public void NotifyNodeGeometryChanged(DialogToolkitNodeElement nodeElement)
            {
                if (!nodeElements.ContainsKey(nodeElement.Node))
                {
                    return;
                }

                owner.nodeRects[nodeElement.Node] = nodeElement.GetGraphRect();
                RefreshConnectionsFor(nodeElement.Node);
            }

            public void BeginNodeDrag(DialogToolkitNodeElement nodeElement, PointerDownEvent evt)
            {
                if (owner.isSelectingTargetPhrase)
                {
                    return;
                }

                draggedNode = nodeElement.Node;
                SelectNode(nodeElement.Node);
                nodeElement.BeginDrag(evt);
            }

            public void MoveNode(DialogToolkitNodeElement nodeElement, Vector2 graphPosition)
            {
                if (draggedNode != nodeElement.Node)
                {
                    return;
                }

                nodeElement.Node.Position = ClampNodePosition(graphPosition);
                nodeElement.SetGraphPosition(nodeElement.Node.Position);
                owner.nodeRects[nodeElement.Node] = nodeElement.GetGraphRect();
                RefreshConnectionsFor(nodeElement.Node);
            }

            public void EndNodeDrag(DialogToolkitNodeElement nodeElement)
            {
                if (draggedNode != nodeElement.Node)
                {
                    return;
                }

                draggedNode = null;
                owner.nodeRects[nodeElement.Node] = nodeElement.GetGraphRect();
                owner.MarkDirty(owner.currentGraph);
                RefreshConnectionsFor(nodeElement.Node);
            }

            private void SelectNode(DialogNode node)
            {
                if (owner.activeConnectionNode == node)
                {
                    return;
                }

                owner.activeConnectionNode = node;
                RefreshConnections();
            }

            private void RefreshConnectionsFor(DialogNode node)
            {
                foreach (DialogToolkitConnectionElement connectionElement in connectionElements.Values)
                {
                    if (connectionElement.IsConnectedTo(node))
                    {
                        connectionElement.MarkDirtyRepaint();
                    }
                }
            }

            private void RefreshConnections()
            {
                foreach (DialogToolkitConnectionElement connectionElement in connectionElements.Values)
                {
                    connectionElement.MarkDirtyRepaint();
                }
            }

            private void HandlePointerDown(PointerDownEvent evt)
            {
                if (evt.button == 0 && (evt.target == this || evt.target == graphContent))
                {
                    if (owner.activeConnectionNode != null)
                    {
                        owner.activeConnectionNode = null;
                        RefreshConnections();
                    }

                    return;
                }

                if (evt.button != 1 || isPanning)
                {
                    return;
                }

                isPanning = true;
                panPointerId = evt.pointerId;
                panStartPointer = evt.position;
                panStartOffset = owner.panOffset;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }

            private void HandlePointerMove(PointerMoveEvent evt)
            {
                if (!isPanning || evt.pointerId != panPointerId || !this.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
                owner.panOffset = panStartOffset + (pointerPosition - panStartPointer);
                owner.ClampPanToWorkspace(WorkspaceWidth, WorkspaceHeight);
                ApplyViewTransform();
                evt.StopPropagation();
            }

            private void HandlePointerUp(PointerUpEvent evt)
            {
                EndPan(evt.pointerId);
            }

            private void HandlePointerCaptureOut(PointerCaptureOutEvent evt)
            {
                EndPan(evt.pointerId);
            }

            private void EndPan(int pointerId)
            {
                if (!isPanning || pointerId != panPointerId)
                {
                    return;
                }

                if (this.HasPointerCapture(pointerId))
                {
                    this.ReleasePointer(pointerId);
                }

                isPanning = false;
                panPointerId = -1;
            }

            private void HandleWheel(WheelEvent evt)
            {
                float zoomDelta = -evt.delta.y * 0.05f;
                float oldZoom = owner.zoom;
                float newZoom = Mathf.Clamp(owner.zoom + zoomDelta, ZoomMin, ZoomMax);
                if (Mathf.Approximately(oldZoom, newZoom))
                {
                    return;
                }

                Vector2 graphPoint = (evt.mousePosition - owner.panOffset) / oldZoom;
                owner.zoom = newZoom;
                owner.panOffset = evt.mousePosition - graphPoint * newZoom;
                owner.ClampPanToWorkspace(WorkspaceWidth, WorkspaceHeight);
                ApplyViewTransform();
                evt.StopPropagation();
            }

            private void ApplyViewTransform()
            {
                graphContent.style.left = owner.panOffset.x;
                graphContent.style.top = owner.panOffset.y;
                graphContent.style.scale = new Scale(new Vector2(owner.zoom, owner.zoom));
            }

            private static Vector2 ClampNodePosition(Vector2 position)
            {
                return new Vector2(
                    Mathf.Clamp(position.x, 0f, WorkspaceWidth - DialogNodeWidth),
                    Mathf.Clamp(position.y, 0f, WorkspaceHeight - NodeHeaderHeight));
            }
        }

        private sealed class DialogToolkitNodeElement : VisualElement
        {
            private readonly DialogToolkitCanvas canvas;
            private readonly VisualElement header;
            private readonly Label title;
            private readonly IMGUIContainer content;
            private int dragPointerId = -1;
            private Vector2 dragStartPointer;
            private Vector2 dragStartPosition;

            public DialogToolkitNodeElement(DialogToolkitCanvas canvas, DialogNode node)
            {
                this.canvas = canvas;
                Node = node;
                name = "dialog-toolkit-node";
                style.position = Position.Absolute;
                style.left = node.Position.x;
                style.top = node.Position.y;
                style.width = DialogNodeWidth;
                style.minHeight = 80f;
                style.flexDirection = FlexDirection.Column;
                style.borderTopWidth = 1f;
                style.borderBottomWidth = 1f;
                style.borderLeftWidth = 1f;
                style.borderRightWidth = 1f;
                style.borderTopLeftRadius = 4f;
                style.borderTopRightRadius = 4f;
                style.borderBottomLeftRadius = 4f;
                style.borderBottomRightRadius = 4f;

                header = new VisualElement
                {
                    name = "dialog-toolkit-node-header"
                };
                header.style.height = 24f;
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                header.style.paddingLeft = 7f;
                header.style.paddingRight = 3f;
                header.style.borderTopLeftRadius = 3f;
                header.style.borderTopRightRadius = 3f;
                hierarchy.Add(header);

                title = new Label
                {
                    name = "dialog-toolkit-node-title"
                };
                title.style.flexGrow = 1f;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.whiteSpace = WhiteSpace.NoWrap;
                title.style.overflow = Overflow.Hidden;
                title.style.textOverflow = TextOverflow.Ellipsis;
                header.Add(title);

                var removeButton = new Button(() => canvas.OwnerDeleteNode(Node))
                {
                    text = "×",
                    name = "dialog-toolkit-node-remove"
                };
                removeButton.style.width = 20f;
                removeButton.style.height = 18f;
                removeButton.style.paddingLeft = 0f;
                removeButton.style.paddingRight = 0f;
                header.Add(removeButton);

                content = new IMGUIContainer(() => canvas.OwnerDrawNode(Node))
                {
                    name = "dialog-toolkit-node-content"
                };
                content.style.flexGrow = 1f;
                content.style.paddingLeft = 5f;
                content.style.paddingRight = 5f;
                content.style.paddingBottom = 5f;
                hierarchy.Add(content);

                header.RegisterCallback<PointerDownEvent>(HandleHeaderPointerDown);
                header.RegisterCallback<PointerMoveEvent>(HandleHeaderPointerMove);
                header.RegisterCallback<PointerUpEvent>(HandleHeaderPointerUp);
                header.RegisterCallback<PointerCaptureOutEvent>(HandleHeaderPointerCaptureOut);
                RegisterCallback<GeometryChangedEvent>(_ => canvas.NotifyNodeGeometryChanged(this));
                RefreshAppearance();
            }

            public DialogNode Node { get; }

            public Rect GetGraphRect()
            {
                float width = layout.width > 0f ? layout.width : DialogNodeWidth;
                float height = layout.height > 0f ? layout.height : 220f;
                return new Rect(Node.Position, new Vector2(width, height));
            }

            public void SetGraphPosition(Vector2 position)
            {
                style.left = position.x;
                style.top = position.y;
            }

            public void RefreshAppearance()
            {
                title.text = canvas.OwnerGetNodeTitle(Node);
                Color tint = canvas.OwnerGetNodeTint(Node);
                header.style.backgroundColor = tint;
                header.style.color = Color.black;
                style.backgroundColor = canvas.OwnerPanelBackgroundColor;
                style.borderTopColor = canvas.OwnerMinorGridColor;
                style.borderBottomColor = canvas.OwnerMinorGridColor;
                style.borderLeftColor = canvas.OwnerMinorGridColor;
                style.borderRightColor = canvas.OwnerMinorGridColor;
                content.MarkDirtyRepaint();
            }

            public void RefreshTargetSelection()
            {
                if (!canvas.OwnerIsSelectingTarget)
                {
                    style.opacity = 1f;
                    Color defaultBorderColor = canvas.OwnerMinorGridColor;
                    style.borderTopColor = defaultBorderColor;
                    style.borderBottomColor = defaultBorderColor;
                    style.borderLeftColor = defaultBorderColor;
                    style.borderRightColor = defaultBorderColor;
                    return;
                }

                style.opacity = Node.Phrase == null ? 0.45f : 1f;
                Color selectionBorderColor = canvas.OwnerSelectionBorderColor;
                style.borderTopColor = selectionBorderColor;
                style.borderBottomColor = selectionBorderColor;
                style.borderLeftColor = selectionBorderColor;
                style.borderRightColor = selectionBorderColor;
            }

            public void BeginDrag(PointerDownEvent evt)
            {
                dragPointerId = evt.pointerId;
                dragStartPointer = evt.position;
                dragStartPosition = Node.Position;
                header.CapturePointer(evt.pointerId);
            }

            private void HandleHeaderPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0 || evt.target is Button)
                {
                    return;
                }

                canvas.BeginNodeDrag(this, evt);
                evt.StopPropagation();
            }

            private void HandleHeaderPointerMove(PointerMoveEvent evt)
            {
                if (evt.pointerId != dragPointerId || !header.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
                Vector2 graphPosition = dragStartPosition + (pointerPosition - dragStartPointer) / canvas.OwnerZoom;
                canvas.MoveNode(this, graphPosition);
                evt.StopPropagation();
            }

            private void HandleHeaderPointerUp(PointerUpEvent evt)
            {
                EndDrag(evt.pointerId);
                evt.StopPropagation();
            }

            private void HandleHeaderPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                EndDrag(evt.pointerId);
            }

            private void EndDrag(int pointerId)
            {
                if (pointerId != dragPointerId)
                {
                    return;
                }

                if (header.HasPointerCapture(pointerId))
                {
                    header.ReleasePointer(pointerId);
                }

                dragPointerId = -1;
                canvas.EndNodeDrag(this);
            }
        }

        private sealed class DialogToolkitConnectionElement : VisualElement
        {
            private readonly DialogToolkitCanvas canvas;
            private readonly DialogNode sourceNode;
            private readonly DialogNode targetNode;
            private readonly DialogAnswer answer;

            public DialogToolkitConnectionElement(
                DialogToolkitCanvas canvas,
                DialogNode sourceNode,
                DialogNode targetNode,
                DialogAnswer answer)
            {
                this.canvas = canvas;
                this.sourceNode = sourceNode;
                this.targetNode = targetNode;
                this.answer = answer;
                name = "dialog-toolkit-connection";
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;
                style.width = WorkspaceWidth;
                style.height = WorkspaceHeight;
                generateVisualContent += DrawConnection;
            }

            public bool IsConnectedTo(DialogNode node)
            {
                return sourceNode == node || targetNode == node;
            }

            private void DrawConnection(MeshGenerationContext context)
            {
                if (!canvas.OwnerTryGetNodeRect(sourceNode, out Rect sourceRect) ||
                    !canvas.OwnerTryGetNodeRect(targetNode, out Rect targetRect))
                {
                    return;
                }

                Vector2 startPos = new Vector2(sourceRect.xMax - 12f, sourceRect.center.y);
                Vector2 endPos = DialogEditorWindow.GetNearestSideCenter(targetRect, startPos);
                Vector2 endDirection = DialogEditorWindow.GetConnectionDirectionForRectPoint(targetRect, endPos);
                Vector2 startTangent = startPos + Vector2.right * 60f;
                Vector2 endTangent = endPos + endDirection * 60f;

                if (!canvas.IsDraggingNode(sourceNode) && !canvas.IsDraggingNode(targetNode))
                {
                    (startTangent, endTangent) = canvas.OwnerGetConnectionTangents(
                        answer,
                        startPos,
                        endPos,
                        sourceRect,
                        targetRect,
                        sourceNode,
                        targetNode);
                }

                Color color = canvas.OwnerGetConnectionColor(sourceNode, targetNode);
                Painter2D painter = context.painter2D;
                painter.strokeColor = color;
                painter.lineWidth = 3f;
                painter.BeginPath();
                painter.MoveTo(startPos);
                painter.BezierCurveTo(startTangent, endTangent, endPos);
                painter.Stroke();

                Vector2 direction = endPos - endTangent;
                Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
                Vector2 right = new Vector2(-normalizedDirection.y, normalizedDirection.x);
                Vector2 arrowBase = endPos - normalizedDirection * 18f;
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(endPos);
                painter.LineTo(arrowBase + right * 7.5f);
                painter.LineTo(arrowBase - right * 7.5f);
                painter.ClosePath();
                painter.Fill();
            }
        }

        private sealed class DialogToolkitGridElement : VisualElement
        {
            private readonly DialogEditorWindow owner;

            public DialogToolkitGridElement(DialogEditorWindow owner)
            {
                this.owner = owner;
                name = "dialog-toolkit-grid";
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;
                style.width = WorkspaceWidth;
                style.height = WorkspaceHeight;
                generateVisualContent += DrawGrid;
            }

            private void DrawGrid(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                DrawGridLines(painter, 40f, owner.MinorGridColor, 1f);
                DrawGridLines(painter, 200f, owner.MajorGridColor, 1.4f);
            }

            private static void DrawGridLines(Painter2D painter, float step, Color color, float width)
            {
                painter.strokeColor = color;
                painter.lineWidth = width;

                for (float x = 0f; x <= WorkspaceWidth; x += step)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, 0f));
                    painter.LineTo(new Vector2(x, WorkspaceHeight));
                    painter.Stroke();
                }

                for (float y = 0f; y <= WorkspaceHeight; y += step)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(0f, y));
                    painter.LineTo(new Vector2(WorkspaceWidth, y));
                    painter.Stroke();
                }
            }
        }

        private readonly struct CachedConnectionRoute
        {
            public CachedConnectionRoute(
                int layoutVersion,
                Vector2 startPos,
                Rect sourceRect,
                Rect targetRect,
                Vector2 startTangent,
                Vector2 endTangent)
            {
                LayoutVersion = layoutVersion;
                StartPos = startPos;
                SourceRect = sourceRect;
                TargetRect = targetRect;
                StartTangent = startTangent;
                EndTangent = endTangent;
            }

            public int LayoutVersion { get; }
            public Vector2 StartPos { get; }
            public Rect SourceRect { get; }
            public Rect TargetRect { get; }
            public Vector2 StartTangent { get; }
            public Vector2 EndTangent { get; }
        }

        private readonly struct ConnectionPort
        {
            public ConnectionPort(Vector2 edgePoint, Vector2 outerPoint)
            {
                EdgePoint = edgePoint;
                OuterPoint = outerPoint;
            }

            public Vector2 EdgePoint { get; }
            public Vector2 OuterPoint { get; }
        }

        private void DrawTargetSelectionOverlay(Rect visibleGraphRect)
        {
            if (!isSelectingTargetPhrase || pendingAnswer == null)
            {
                return;
            }

            Handles.BeginGUI();
            Vector2 graphMousePosition = GetGraphMousePosition(Event.current.mousePosition);

            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node.Phrase == null)
                {
                    continue;
                }

                if (!nodeRects.TryGetValue(node, out Rect rect) ||
                    !GraphEditorCanvasUtility.IsAtLeastPartiallyVisible(rect, visibleGraphRect))
                {
                    continue;
                }

                bool isHovered = rect.Contains(graphMousePosition);
                EditorGUI.DrawRect(rect, GetSelectionOverlayColor(isHovered));

                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 52,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = SelectionOverlayTextColor }
                };

                GUI.Label(rect, "+", style);

                if ((Event.current.rawType == EventType.MouseDown || Event.current.rawType == EventType.MouseUp) &&
                    rect.Contains(graphMousePosition))
                {
                    pendingAnswer.SetNextPhrase(node.Phrase);
                    MarkDirty(sourcePhraseForSelection);
                    InvalidateGraphCaches();
                    CancelTargetSelection(false);
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            Handles.EndGUI();
        }

        private Vector2 GetGraphMousePosition(Vector2 mousePosition)
        {
            return new Vector2(
                (mousePosition.x - panOffset.x) / zoom,
                (mousePosition.y - panOffset.y) / zoom);
        }

        private void ClampPanToWorkspace(float workspaceWidth, float workspaceHeight)
        {
            float viewWidth = position.width;
            float viewHeight = position.height;

            float minX = viewWidth - workspaceWidth * zoom;
            float maxX = 0f;
            float minY = viewHeight - workspaceHeight * zoom;
            float maxY = 0f;

            if (workspaceWidth * zoom <= viewWidth)
            {
                panOffset.x = Mathf.Round((viewWidth - workspaceWidth * zoom) * 0.5f);
            }
            else
            {
                panOffset.x = Mathf.Clamp(panOffset.x, minX, maxX);
            }

            if (workspaceHeight * zoom <= viewHeight)
            {
                panOffset.y = Mathf.Round((viewHeight - workspaceHeight * zoom) * 0.5f);
            }
            else
            {
                panOffset.y = Mathf.Clamp(panOffset.y, minY, maxY);
            }
        }

        private void DrawNodeWindow(DialogNode node, bool allowWindowDrag = true)
        {
            if (TryHandleTargetPhraseSelection(node))
            {
                return;
            }

            if (!isSelectingTargetPhrase &&
                Event.current.rawType == EventType.MouseDown &&
                Event.current.button == 0 &&
                activeConnectionNode != node)
            {
                activeConnectionNode = node;
                RefreshToolkitCanvas();
                Repaint();
            }

            EditorGUI.BeginDisabledGroup(isSelectingTargetPhrase);

            Rect removeButtonRect = new Rect(298f, 5f, 16f, 16f);
            if (allowWindowDrag && DrawMiniButton(removeButtonRect, "x"))
            {
                DeleteNode(node);
                EditorGUI.EndDisabledGroup();
                return;
            }

            EditorGUI.BeginChangeCheck();
            List<EditorStyleTextOverride> objectFieldOverrides = useLightTheme && Event.current.type == EventType.Repaint
                ? CreateTemporaryObjectFieldTextOverrides(Color.white)
                : null;
            DialogPhrase newPhrase;

            try
            {
                newPhrase = (DialogPhrase)EditorGUILayout.ObjectField(node.Phrase, typeof(DialogPhrase), false);
            }
            finally
            {
                RestoreTemporaryStyleTextOverrides(objectFieldOverrides);
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (newPhrase != null && currentGraph.Nodes.Exists(n => n != node && n.Phrase == newPhrase))
                {
                    EditorUtility.DisplayDialog(
                        "Duplicate Phrase Detected",
                        $"Phrase \"{newPhrase.name}\" is already assigned to another node.",
                        "OK");
                }
                else
                {
                    if (currentGraph.IsEntryPhrase(node.Phrase))
                    {
                        currentGraph.SetEntryPhrase(newPhrase);
                    }

                    InvalidatePhraseDisplayName(node.Phrase);
                    InvalidatePhraseDisplayName(newPhrase);
                    ReplacePhraseReferences(node.Phrase, newPhrase);
                    node.Phrase = newPhrase;
                    MarkDirty(currentGraph);
                    InvalidateGraphStructure();
                }
            }

            if (node.Phrase == null)
            {
                EditorGUILayout.HelpBox("No phrase assigned.", MessageType.Warning);
                if (allowWindowDrag)
                {
                    GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                }

                EditorGUI.EndDisabledGroup();
                return;
            }

            DrawPhraseEditor(node.Phrase);

            if (DrawButton(currentGraph.IsEntryPhrase(node.Phrase) ? "Start Phrase" : "Set As Start"))
            {
                currentGraph.SetEntryPhrase(node.Phrase);
                MarkDirty(currentGraph);
                InvalidateGraphCaches();
            }

            if (node.Phrase.IsQuestPhrase)
            {
                EditorGUILayout.HelpBox(
                    "Quest phrase appears as an answer on the start phrase and does not require incoming links.",
                    MessageType.Info);
            }

            if (IsOrphanPhrase(node.Phrase))
            {
                EditorGUILayout.HelpBox(
                    "This phrase has no incoming answers and is not the entry phrase.",
                    MessageType.Warning);
            }

            EditorGUI.EndDisabledGroup();
            if (allowWindowDrag)
            {
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
            }
        }

        private bool TryHandleTargetPhraseSelection(DialogNode node)
        {
            if (!isSelectingTargetPhrase || pendingAnswer == null)
            {
                return false;
            }

            if (node.Phrase == null)
            {
                return false;
            }

            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
            {
                return false;
            }

            pendingAnswer.SetNextPhrase(node.Phrase);
            MarkDirty(sourcePhraseForSelection);
            InvalidateGraphCaches();
            CancelTargetSelection(false);
            Event.current.Use();
            GUIUtility.ExitGUI();
            return true;
        }

        private void DrawPhraseEditor(DialogPhrase phrase)
        {
            SerializedObject phraseObject = new SerializedObject(phrase);
            phraseObject.Update();

            SerializedProperty textProperty = phraseObject.FindProperty("text");
            SerializedProperty isForcedDialoguePhraseProperty = phraseObject.FindProperty("isForcedDialoguePhrase");
            SerializedProperty forcedDialoguePriorityProperty = phraseObject.FindProperty("forcedDialoguePriority");
            SerializedProperty restoresExitAbilityProperty = phraseObject.FindProperty("restoresExitAbility");
            SerializedProperty isQuestPhraseProperty = phraseObject.FindProperty("isQuestPhrase");
            SerializedProperty questAnswerProperty = phraseObject.FindProperty("questAnswer");
            SerializedProperty answersProperty = phraseObject.FindProperty("answers");

            if (answersProperty == null)
            {
                EditorGUILayout.HelpBox("Answers property was not found.", MessageType.Error);
                return;
            }

            if (textProperty != null)
            {
                DrawLocalizedStringSelector(textProperty, "Phrase");
            }

            if (isForcedDialoguePhraseProperty != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.PropertyField(isForcedDialoguePhraseProperty, new GUIContent("Forced Dialogue Phrase"));

                if (isForcedDialoguePhraseProperty.boolValue)
                {
                    if (forcedDialoguePriorityProperty != null)
                    {
                        EditorGUILayout.PropertyField(
                            forcedDialoguePriorityProperty,
                            new GUIContent(
                                "Priority",
                                "When several forced dialogue phrases are available, the phrase with the lower value starts first. Equal priorities keep graph order."));
                    }

                    if (restoresExitAbilityProperty != null)
                    {
                        restoresExitAbilityProperty.boolValue = false;
                    }
                }
                else if (restoresExitAbilityProperty != null)
                {
                    bool canRestoreExitAbility = CanRestoreExitAbility(phrase);
                    if (!canRestoreExitAbility)
                    {
                        restoresExitAbilityProperty.boolValue = false;
                    }

                    EditorGUI.BeginDisabledGroup(!canRestoreExitAbility);
                    EditorGUILayout.PropertyField(restoresExitAbilityProperty, new GUIContent("Restores Ability To Exit"));
                    EditorGUI.EndDisabledGroup();

                    if (!canRestoreExitAbility)
                    {
                        EditorGUILayout.HelpBox(
                            "This option is available only on a branch after a forced dialogue phrase, before exit has already been restored.",
                            MessageType.Info);
                    }
                }
            }

            if (isQuestPhraseProperty != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.PropertyField(isQuestPhraseProperty, new GUIContent("Quest Phrase"));

                if (isQuestPhraseProperty.boolValue)
                {
                    DrawQuestPhraseSettings(questAnswerProperty);
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Answers", MiniBoldLabelStyle);

            int removeAnswerIndex = -1;
            DialogNode ownerNode = currentGraph.Nodes.FirstOrDefault(n => n.Phrase == phrase);

            for (int i = 0; i < answersProperty.arraySize; i++)
            {
                SerializedProperty answerProperty = answersProperty.GetArrayElementAtIndex(i);
                SerializedProperty answerTextProperty = answerProperty.FindPropertyRelative("text");
                SerializedProperty nextPhraseProperty = answerProperty.FindPropertyRelative("nextPhrase");
                SerializedProperty hasConditionsProperty = answerProperty.FindPropertyRelative("hasConditions");
                SerializedProperty conditionsProperty = answerProperty.FindPropertyRelative("conditions");
                DialogAnswer answer = i < phrase.Answers.Count ? phrase.Answers[i] : null;
                DialogPhrase nextPhrase = answer?.NextPhrase;

                bool missingLink = nextPhrase == null;
                bool targetOutsideGraph = nextPhrase != null && !ContainsPhrase(nextPhrase);
                bool hasConditions = hasConditionsProperty != null && hasConditionsProperty.boolValue;
                int conditionCount = conditionsProperty?.arraySize ?? 0;
                string foldoutKey = GetAnswerFoldoutKey(phrase, i);
                bool isExpanded = GetAnswerFoldoutState(foldoutKey);
                Color accentColor = GetAnswerAccentColor(missingLink, targetOutsideGraph, hasConditions);
                string statusLabel = GetAnswerStatusLabel(missingLink, targetOutsideGraph, hasConditions, conditionCount);

                EditorGUILayout.BeginHorizontal(HelpBoxStyle);

                Rect accentRect = GUILayoutUtility.GetRect(
                    AccentLineWidth,
                    AccentLineWidth,
                    GUILayout.Width(AccentLineWidth),
                    GUILayout.ExpandHeight(true));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(accentRect, accentColor);
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                bool newExpanded = EditorGUILayout.Foldout(isExpanded, $"Answer {i + 1}", true, FoldoutStyle);
                if (newExpanded != isExpanded)
                {
                    SetAnswerFoldoutState(foldoutKey, newExpanded);
                    isExpanded = newExpanded;
                    InvalidateNodeLayout(phrase);
                }

                GUILayout.Label(statusLabel, CenteredMiniLabelStyle, GUILayout.Width(110f));
                GUILayout.FlexibleSpace();

                if (DrawMiniButton("X", GUILayout.Width(22f)))
                {
                    removeAnswerIndex = i;
                }

                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = missingLink ? DangerButtonColor : LinkButtonColor;
                bool pickPressed = DrawMiniButton("O", GUILayout.Width(22f));
                GUI.backgroundColor = previousBackground;

                Rect localButtonRect = GUILayoutUtility.GetLastRect();
                if (ownerNode != null && nodeRects.TryGetValue(ownerNode, out Rect nodeRect) && i < phrase.Answers.Count)
                {
                    Vector2 localCenter = new Vector2(
                        localButtonRect.x + localButtonRect.width * 0.5f,
                        localButtonRect.y + localButtonRect.height * 0.5f);
                    answerAnchorPositions[phrase.Answers[i]] = nodeRect.position + localCenter;
                }

                if (pickPressed && i < phrase.Answers.Count)
                {
                    isSelectingTargetPhrase = true;
                    pendingAnswer = phrase.Answers[i];
                    sourcePhraseForSelection = phrase;
                    toolkitCanvas?.RefreshTargetSelection();
                }

                if (isExpanded)
                {
                    EditorGUILayout.EndHorizontal();
                    DrawAnswerDivider(StrongDividerColor);
                    EditorGUILayout.Space(3f);

                    if (answerTextProperty != null)
                    {
                        DrawLocalizedStringSelector(answerTextProperty, "Text");
                    }

                    if (hasConditionsProperty != null)
                    {
                        EditorGUILayout.PropertyField(hasConditionsProperty, new GUIContent("Condition"));
                        if (hasConditionsProperty.boolValue && conditionsProperty != null)
                        {
                            DrawDialogAnswerConditions(conditionsProperty);
                            DrawAnswerQuestLinksSummary(conditionsProperty);
                        }
                    }

                    if (nextPhraseProperty != null)
                    {
                        DrawPropertyFieldWithCustomLabel(nextPhraseProperty, "Next Phrase");
                    }

                    if (missingLink)
                    {
                        EditorGUILayout.HelpBox("Next phrase is not assigned for this answer.", MessageType.Error);
                    }
                    else if (targetOutsideGraph)
                    {
                        EditorGUILayout.HelpBox("Target phrase is not added to the current dialog graph.", MessageType.Warning);
                    }
                }
                else
                {
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (i < answersProperty.arraySize - 1)
                {
                    EditorGUILayout.Space(3f);
                    DrawAnswerDivider(SoftDividerColor);
                    EditorGUILayout.Space(5f);
                }
                else
                {
                    EditorGUILayout.Space(4f);
                }
            }

            if (DrawButton("+ Add Answer"))
            {
                answersProperty.arraySize++;
                ClearAnswerFoldoutStates(phrase);
                InvalidateNodeLayout(phrase);
            }

            if (removeAnswerIndex >= 0)
            {
                answersProperty.DeleteArrayElementAtIndex(removeAnswerIndex);
                ClearAnswerFoldoutStates(phrase);
                InvalidateNodeLayout(phrase);
            }

            if (phraseObject.hasModifiedProperties)
            {
                phraseObject.ApplyModifiedProperties();
                MarkDirty(phrase);
                InvalidatePhraseDisplayName(phrase);
                InvalidateGraphCaches();
                InvalidateNodeLayout(phrase);
            }
        }

        private void DrawQuestPhraseSettings(SerializedProperty questAnswerProperty)
        {
            if (questAnswerProperty == null)
            {
                EditorGUILayout.HelpBox("Quest answer property was not found.", MessageType.Error);
                return;
            }

            SerializedProperty answerTextProperty = questAnswerProperty.FindPropertyRelative("text");
            SerializedProperty hasConditionsProperty = questAnswerProperty.FindPropertyRelative("hasConditions");
            SerializedProperty conditionsProperty = questAnswerProperty.FindPropertyRelative("conditions");

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Quest Entry Answer", MiniBoldLabelStyle);
            EditorGUILayout.BeginVertical(HelpBoxStyle);
            EditorGUILayout.LabelField(
                "This answer is shown on the start phrase automatically when its conditions are satisfied.",
                WordWrappedMiniLabelStyle);
            EditorGUILayout.Space(3f);

            DrawAnswerDetails(
                answerTextProperty,
                hasConditionsProperty,
                conditionsProperty,
                "Answer Text");

            EditorGUILayout.EndVertical();
        }

        private void DrawAnswerDetails(
            SerializedProperty answerTextProperty,
            SerializedProperty hasConditionsProperty,
            SerializedProperty conditionsProperty,
            string textLabel)
        {
            if (answerTextProperty != null)
            {
                DrawLocalizedStringSelector(answerTextProperty, textLabel);
            }

            if (hasConditionsProperty == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(hasConditionsProperty, new GUIContent("Condition"));
            if (!hasConditionsProperty.boolValue || conditionsProperty == null)
            {
                return;
            }

            DrawDialogAnswerConditions(conditionsProperty);
            DrawAnswerQuestLinksSummary(conditionsProperty);
        }

        private string GetAnswerFoldoutKey(DialogPhrase phrase, int answerIndex)
        {
            return $"{phrase.GetInstanceID()}:{answerIndex}";
        }

        private bool GetAnswerFoldoutState(string foldoutKey)
        {
            if (answerFoldoutStates.TryGetValue(foldoutKey, out bool isExpanded))
            {
                return isExpanded;
            }

            answerFoldoutStates[foldoutKey] = false;
            return false;
        }

        private bool CanRestoreExitAbility(DialogPhrase phrase)
        {
            if (phrase == null || currentGraph == null)
            {
                return false;
            }

            if (!restoreExitAbilityCache.TryGetValue(phrase, out bool canRestoreExitAbility))
            {
                canRestoreExitAbility = currentGraph.CanRestoreExitAbility(phrase);
                restoreExitAbilityCache[phrase] = canRestoreExitAbility;
            }

            return canRestoreExitAbility;
        }

        private void SetAnswerFoldoutState(string foldoutKey, bool isExpanded)
        {
            answerFoldoutStates[foldoutKey] = isExpanded;
        }

        private void InvalidateNodeLayout(DialogPhrase phrase)
        {
            if (phrase == null)
            {
                return;
            }

            phrasesWithDirtyLayout.Add(phrase);
            phrasesAwaitingRepaintAfterLayout.Remove(phrase);
            Repaint();
        }

        private void ClearAnswerFoldoutStates(DialogPhrase phrase)
        {
            if (phrase == null)
            {
                return;
            }

            string keyPrefix = $"{phrase.GetInstanceID()}:";
            List<string> keysToRemove = answerFoldoutStates.Keys
                .Where(key => key.StartsWith(keyPrefix, StringComparison.Ordinal))
                .ToList();

            foreach (string key in keysToRemove)
            {
                answerFoldoutStates.Remove(key);
            }
        }

        private Color GetAnswerAccentColor(bool missingLink, bool targetOutsideGraph, bool hasConditions)
        {
            if (missingLink)
            {
                return DangerAccentColor;
            }

            if (targetOutsideGraph)
            {
                return WarningAccentColor;
            }

            if (hasConditions)
            {
                return ConditionAccentColor;
            }

            return RewardAccentColor;
        }

        private static string GetAnswerStatusLabel(bool missingLink, bool targetOutsideGraph, bool hasConditions, int conditionCount)
        {
            if (missingLink)
            {
                return "Missing Next";
            }

            if (targetOutsideGraph)
            {
                return "Outside Graph";
            }

            if (hasConditions)
            {
                return conditionCount > 0
                    ? $"Conditions: {conditionCount}"
                    : "Has Conditions";
            }

            return "Linked";
        }

        private void DrawAnswerDivider(Color color)
        {
            Rect dividerRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(dividerRect, color);
        }

        private Color GetConditionAccentColor(DialogAnswerConditionType conditionType)
        {
            return ItemAccentColor;
        }

        private static string GetConditionTitle(SerializedProperty typeProperty)
        {
            if (typeProperty == null || typeProperty.propertyType != SerializedPropertyType.Enum)
            {
                return "Condition";
            }

            string[] displayNames = typeProperty.enumDisplayNames;
            int index = typeProperty.enumValueIndex;
            if (displayNames == null || index < 0 || index >= displayNames.Length)
            {
                return "Condition";
            }

            return displayNames[index];
        }

        private void DrawDialogAnswerConditions(SerializedProperty conditionsProperty)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Conditions / Actions", MiniBoldLabelStyle);

            int removeIndex = -1;
            for (int i = 0; i < conditionsProperty.arraySize; i++)
            {
                SerializedProperty conditionProperty = conditionsProperty.GetArrayElementAtIndex(i);
                SerializedProperty typeProperty = conditionProperty.FindPropertyRelative("type");
                SerializedProperty moneyAmountProperty = conditionProperty.FindPropertyRelative("moneyAmount");
                SerializedProperty itemConfigProperty = conditionProperty.FindPropertyRelative("itemConfig");
                SerializedProperty itemCountProperty = conditionProperty.FindPropertyRelative("itemCount");
                SerializedProperty questGraphProperty = conditionProperty.FindPropertyRelative("questGraph");
                SerializedProperty questSourceNodeProperty = conditionProperty.FindPropertyRelative("questSourceNode");
                SerializedProperty questTransitionProperty = conditionProperty.FindPropertyRelative("questTransition");
                SerializedProperty questNodeProperty = conditionProperty.FindPropertyRelative("questNode");
                DialogAnswerConditionType conditionType = (DialogAnswerConditionType)typeProperty.enumValueIndex;
                Color accentColor = GetConditionAccentColor(conditionType);
                string conditionTitle = GetConditionTitle(typeProperty);

                EditorGUILayout.BeginHorizontal(HelpBoxStyle);
                Rect accentRect = GUILayoutUtility.GetRect(
                    AccentLineWidth,
                    AccentLineWidth,
                    GUILayout.Width(AccentLineWidth),
                    GUILayout.ExpandHeight(true));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(accentRect, accentColor);
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entry {i + 1}", MiniBoldLabelStyle, GUILayout.Width(52f));
                GUILayout.Label(conditionTitle, CenteredMiniLabelStyle);
                GUILayout.FlexibleSpace();
                if (DrawMiniButton("X", GUILayout.Width(22f)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();
                DrawAnswerDivider(SectionDividerColor);
                EditorGUILayout.Space(3f);

                DrawEnumPropertyField(typeProperty, "Type");

                switch (conditionType)
                {
                    case DialogAnswerConditionType.GiveMoney:
                    case DialogAnswerConditionType.TakeMoney:
                    case DialogAnswerConditionType.TakeMoneyMax:
                        DrawPropertyFieldWithCustomLabel(moneyAmountProperty, "Money");
                        break;
                    case DialogAnswerConditionType.TakeItemIfHas:
                        DrawPropertyFieldWithCustomLabel(itemConfigProperty, "Item");
                        DrawPropertyFieldWithCustomLabel(itemCountProperty, "Count");
                        break;
                    case DialogAnswerConditionType.CheckQuestStep:
                    case DialogAnswerConditionType.DoQuestStep:
                        DrawQuestTransitionSelector(
                            conditionsProperty,
                            i,
                            questGraphProperty,
                            questSourceNodeProperty,
                            questTransitionProperty);
                        break;
                    case DialogAnswerConditionType.AddQuest:
                        DrawQuestGraphSelector(conditionsProperty, i, questGraphProperty);
                        QuestPreviewUtility.DrawQuestGraphPreview(questGraphProperty.objectReferenceValue as QuestGraph, "Quest");
                        break;
                    case DialogAnswerConditionType.DoQuestEnd:
                        DrawTerminalQuestNodeSelector(conditionsProperty, i, questGraphProperty, questNodeProperty);
                        break;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (i < conditionsProperty.arraySize - 1)
                {
                    EditorGUILayout.Space(2f);
                    DrawAnswerDivider(SoftestDividerColor);
                    EditorGUILayout.Space(4f);
                }
                else
                {
                    EditorGUILayout.Space(2f);
                }
            }

            if (removeIndex >= 0)
            {
                conditionsProperty.DeleteArrayElementAtIndex(removeIndex);
            }

            if (DrawButton("+ Add Condition / Action"))
            {
                conditionsProperty.arraySize++;
            }
        }

        private void DrawAnswerQuestLinksSummary(SerializedProperty conditionsProperty)
        {
            List<string> lines = new();
            for (int i = 0; i < conditionsProperty.arraySize; i++)
            {
                string summary = GetQuestConditionSummary(conditionsProperty.GetArrayElementAtIndex(i));
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    lines.Add(summary);
                }
            }

            if (lines.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Quest Links", MiniBoldLabelStyle);
            EditorGUILayout.BeginVertical(HelpBoxStyle);
            foreach (string line in lines)
            {
                EditorGUILayout.LabelField(line, WordWrappedMiniLabelStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private string GetQuestConditionSummary(SerializedProperty conditionProperty)
        {
            SerializedProperty typeProperty = conditionProperty.FindPropertyRelative("type");
            SerializedProperty questGraphProperty = conditionProperty.FindPropertyRelative("questGraph");
            SerializedProperty questSourceNodeProperty = conditionProperty.FindPropertyRelative("questSourceNode");
            SerializedProperty questTransitionProperty = conditionProperty.FindPropertyRelative("questTransition");
            SerializedProperty questNodeProperty = conditionProperty.FindPropertyRelative("questNode");

            DialogAnswerConditionType type = (DialogAnswerConditionType)typeProperty.enumValueIndex;
            QuestGraph questGraph = questGraphProperty.objectReferenceValue as QuestGraph;
            QuestNodeData questSourceNode = questSourceNodeProperty.objectReferenceValue as QuestNodeData;
            QuestTransition transition = questTransitionProperty.objectReferenceValue as QuestTransition;
            QuestNodeData questNode = questNodeProperty.objectReferenceValue as QuestNodeData;

            return type switch
            {
                DialogAnswerConditionType.AddQuest when questGraph != null =>
                    $"Add Quest -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}",
                DialogAnswerConditionType.CheckQuestStep when questGraph != null && transition != null =>
                    $"Check Transition -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}: {GetQuestTransitionLabel(questGraph, questSourceNode, transition)}",
                DialogAnswerConditionType.DoQuestStep when questGraph != null && transition != null =>
                    $"Execute Transition -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}: {GetQuestTransitionLabel(questGraph, questSourceNode, transition)}",
                DialogAnswerConditionType.DoQuestEnd when questGraph != null && questNode != null =>
                    $"Execute Final Node -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}: {QuestPreviewUtility.GetNodeDisplayName(questNode)}",
                _ => null
            };
        }

        private void DrawQuestGraphSelector(
            SerializedProperty conditionsProperty,
            int currentIndex,
            SerializedProperty questGraphProperty,
            string label = "Quest")
        {
            DrawRelatedQuestShortcuts(conditionsProperty, currentIndex, questGraphProperty);

            List<QuestGraph> questGraphs = GetAllQuestGraphs();
            if (questGraphs.Count == 0)
            {
                questGraphProperty.objectReferenceValue = null;
                EditorGUILayout.HelpBox("No quest graphs were found.", MessageType.Warning);
                return;
            }

            UnityEngine.Object targetObject = conditionsProperty.serializedObject.targetObject;
            QuestGraph currentQuestGraph = questGraphProperty.objectReferenceValue as QuestGraph;
            if (currentQuestGraph != null && !questGraphs.Contains(currentQuestGraph))
            {
                questGraphProperty.objectReferenceValue = null;
                currentQuestGraph = null;
            }

            string buttonLabel = currentQuestGraph == null
                ? $"Select {label}"
                : QuestPreviewUtility.GetQuestDisplayName(currentQuestGraph);

            Rect selectorRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(selectorRect, buttonLabel, PopupStyle))
            {
                OpenQuestGraphSelector(
                    selectorRect,
                    targetObject,
                    questGraphProperty.propertyPath,
                    questGraphs,
                    currentQuestGraph,
                    label);
            }
        }

        private void DrawRelatedQuestShortcuts(
            SerializedProperty conditionsProperty,
            int currentIndex,
            SerializedProperty questGraphProperty)
        {
            List<QuestGraph> relatedGraphs = GetRelatedQuestGraphs(conditionsProperty, currentIndex);
            if (relatedGraphs.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Used In This Answer", MiniLabelStyle);
            EditorGUILayout.BeginHorizontal();
            foreach (QuestGraph relatedGraph in relatedGraphs)
            {
                string label = QuestPreviewUtility.GetQuestDisplayName(relatedGraph);
                if (DrawMiniButton(label))
                {
                    questGraphProperty.objectReferenceValue = relatedGraph;
                    QuestPreviewPopup.ShowQuest(GUILayoutUtility.GetLastRect(), relatedGraph);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private List<QuestGraph> GetRelatedQuestGraphs(SerializedProperty conditionsProperty, int currentIndex)
        {
            var graphs = new List<QuestGraph>();
            for (int i = 0; i < conditionsProperty.arraySize; i++)
            {
                if (i == currentIndex)
                {
                    continue;
                }

                SerializedProperty graphProperty = conditionsProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("questGraph");

                QuestGraph questGraph = graphProperty?.objectReferenceValue as QuestGraph;
                if (questGraph != null && !graphs.Contains(questGraph))
                {
                    graphs.Add(questGraph);
                }
            }

            return graphs;
        }

        private void DrawQuestTransitionSelector(
            SerializedProperty conditionsProperty,
            int currentIndex,
            SerializedProperty questGraphProperty,
            SerializedProperty questSourceNodeProperty,
            SerializedProperty questTransitionProperty)
        {
            List<QuestNodeData> sourceNodes = GetAllQuestSourceNodes();
            if (sourceNodes.Count == 0)
            {
                questGraphProperty.objectReferenceValue = null;
                questSourceNodeProperty.objectReferenceValue = null;
                questTransitionProperty.objectReferenceValue = null;
                EditorGUILayout.HelpBox("No quest nodes with transitions were found.", MessageType.Warning);
                return;
            }

            UnityEngine.Object targetObject = conditionsProperty.serializedObject.targetObject;
            QuestNodeData currentSourceNode = questSourceNodeProperty.objectReferenceValue as QuestNodeData;
            if (currentSourceNode != null && !sourceNodes.Contains(currentSourceNode))
            {
                questSourceNodeProperty.objectReferenceValue = null;
                currentSourceNode = null;
            }

            string sourceNodeLabel = currentSourceNode == null
                ? "Select Source Node"
                : GetQuestNodeOptionLabel(currentSourceNode);

            Rect sourceNodeRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(sourceNodeRect, sourceNodeLabel, PopupStyle))
            {
                OpenSourceNodeSelector(
                    sourceNodeRect,
                    targetObject,
                    questGraphProperty.propertyPath,
                    questSourceNodeProperty.propertyPath,
                    questTransitionProperty.propertyPath,
                    sourceNodes,
                    currentSourceNode);
            }

            currentSourceNode = questSourceNodeProperty.objectReferenceValue as QuestNodeData;
            if (currentSourceNode == null)
            {
                return;
            }

            QuestGraph questGraph = currentSourceNode.OwnerGraph;
            questGraphProperty.objectReferenceValue = questGraph;
            QuestPreviewUtility.DrawQuestNodePreview(currentSourceNode, "Source Node");

            List<QuestTransition> transitions = currentSourceNode.Transitions?
                .Where(transition => transition != null)
                .ToList() ?? new List<QuestTransition>();

            if (transitions.Count == 0)
            {
                questTransitionProperty.objectReferenceValue = null;
                EditorGUILayout.HelpBox("This node has no transitions.", MessageType.Warning);
                return;
            }

            QuestTransition currentTransition = questTransitionProperty.objectReferenceValue as QuestTransition;
            if (currentTransition != null && !transitions.Contains(currentTransition))
            {
                questTransitionProperty.objectReferenceValue = null;
                currentTransition = null;
            }

            string transitionLabel = currentTransition == null
                ? "Select Transition"
                : GetQuestTransitionLabel(questGraph, currentSourceNode, currentTransition);

            Rect transitionRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(transitionRect, transitionLabel, PopupStyle))
            {
                OpenTransitionSelector(
                    transitionRect,
                    targetObject,
                    questTransitionProperty.propertyPath,
                    questGraph,
                    currentSourceNode,
                    transitions,
                    currentTransition);
            }

            QuestPreviewUtility.DrawQuestTransitionPreview(questGraph, questTransitionProperty.objectReferenceValue as QuestTransition, "Transition");
        }

        private void DrawTerminalQuestNodeSelector(
            SerializedProperty conditionsProperty,
            int currentIndex,
            SerializedProperty questGraphProperty,
            SerializedProperty questNodeProperty)
        {
            List<QuestNodeData> terminalNodes = GetAllTerminalQuestNodes();
            if (terminalNodes.Count == 0)
            {
                questGraphProperty.objectReferenceValue = null;
                questNodeProperty.objectReferenceValue = null;
                EditorGUILayout.HelpBox("No terminal quest nodes were found.", MessageType.Warning);
                return;
            }

            UnityEngine.Object targetObject = conditionsProperty.serializedObject.targetObject;
            QuestNodeData currentNode = questNodeProperty.objectReferenceValue as QuestNodeData;
            if (currentNode != null && !terminalNodes.Contains(currentNode))
            {
                questNodeProperty.objectReferenceValue = null;
                currentNode = null;
            }

            string terminalNodeLabel = currentNode == null
                ? "Select Terminal Node"
                : GetQuestNodeOptionLabel(currentNode);

            Rect terminalNodeRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(terminalNodeRect, terminalNodeLabel, PopupStyle))
            {
                OpenTerminalNodeSelector(
                    terminalNodeRect,
                    targetObject,
                    questGraphProperty.propertyPath,
                    questNodeProperty.propertyPath,
                    terminalNodes,
                    currentNode);
            }

            QuestPreviewUtility.DrawQuestNodePreview(questNodeProperty.objectReferenceValue as QuestNodeData, "Quest Node");
        }

        private static QuestNodeData GetOwnerNodeDataForTransition(QuestGraph questGraph, QuestTransition transition)
        {
            if (questGraph == null || transition == null)
            {
                return null;
            }

            return questGraph.Nodes
                .FirstOrDefault(node =>
                    node?.NodeData != null &&
                    node.NodeData.Transitions != null &&
                    node.NodeData.Transitions.Contains(transition))
                ?.NodeData;
        }

        private void OpenSourceNodeSelector(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            string questSourceNodePropertyPath,
            string questTransitionPropertyPath,
            List<QuestNodeData> sourceNodes,
            QuestNodeData currentSourceNode)
        {
            var entries = new List<QuestCardSelectorPopup.Entry>
            {
                new()
                {
                    Title = "<None>",
                    Subtitle = "Clear source node selection",
                    IsSelected = currentSourceNode == null,
                    OnSelect = () =>
                    {
                        ApplySourceNodeSelection(targetObject, questGraphPropertyPath, questSourceNodePropertyPath, questTransitionPropertyPath, null);
                    }
                }
            };

            entries.AddRange(sourceNodes.Select(nodeData => new QuestCardSelectorPopup.Entry
            {
                Title = QuestPreviewUtility.GetNodeDisplayName(nodeData),
                Subtitle = nodeData.OwnerGraph != null ? QuestPreviewUtility.GetQuestDisplayName(nodeData.OwnerGraph) : "Unknown Quest",
                Sprite = nodeData.Icon,
                IsSelected = nodeData == currentSourceNode,
                OnSelect = () =>
                {
                    ApplySourceNodeSelection(targetObject, questGraphPropertyPath, questSourceNodePropertyPath, questTransitionPropertyPath, nodeData);
                }
            }));

            QuestCardSelectorPopup.Show(activatorRect, "Select Source Node", entries);
        }

        private void OpenQuestGraphSelector(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            List<QuestGraph> questGraphs,
            QuestGraph currentQuestGraph,
            string label)
        {
            var entries = new List<QuestCardSelectorPopup.Entry>
            {
                new()
                {
                    Title = "<None>",
                    Subtitle = "Clear quest selection",
                    IsSelected = currentQuestGraph == null,
                    OnSelect = () =>
                    {
                        ApplyQuestGraphSelection(targetObject, questGraphPropertyPath, null);
                    }
                }
            };

            entries.AddRange(questGraphs.Select(questGraph => new QuestCardSelectorPopup.Entry
            {
                Title = QuestPreviewUtility.GetQuestDisplayName(questGraph),
                Subtitle = QuestPreviewUtility.GetQuestDescription(questGraph),
                Sprite = questGraph.GetEntryNode()?.Icon,
                IsSelected = questGraph == currentQuestGraph,
                OnSelect = () =>
                {
                    ApplyQuestGraphSelection(targetObject, questGraphPropertyPath, questGraph);
                }
            }));

            QuestCardSelectorPopup.Show(activatorRect, $"Select {label}", entries);
        }

        private void OpenTransitionSelector(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string questTransitionPropertyPath,
            QuestGraph questGraph,
            QuestNodeData sourceNodeData,
            List<QuestTransition> transitions,
            QuestTransition currentTransition)
        {
            var entries = new List<QuestCardSelectorPopup.Entry>
            {
                new()
                {
                    Title = "<None>",
                    Subtitle = "Clear transition selection",
                    IsSelected = currentTransition == null,
                    OnSelect = () =>
                    {
                        ApplyTransitionSelection(targetObject, questTransitionPropertyPath, null);
                    }
                }
            };

            entries.AddRange(transitions.Select(transition => new QuestCardSelectorPopup.Entry
            {
                Title = GetQuestTransitionLabel(questGraph, sourceNodeData, transition),
                Subtitle = sourceNodeData != null ? QuestPreviewUtility.GetNodeDisplayName(sourceNodeData) : string.Empty,
                Sprite = transition.TargetNode != null ? transition.TargetNode.Icon : null,
                IsSelected = transition == currentTransition,
                OnSelect = () =>
                {
                    ApplyTransitionSelection(targetObject, questTransitionPropertyPath, transition);
                }
            }));

            QuestCardSelectorPopup.Show(activatorRect, "Select Transition", entries);
        }

        private void OpenTerminalNodeSelector(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            string questNodePropertyPath,
            List<QuestNodeData> terminalNodes,
            QuestNodeData currentNode)
        {
            var entries = new List<QuestCardSelectorPopup.Entry>
            {
                new()
                {
                    Title = "<None>",
                    Subtitle = "Clear terminal node selection",
                    IsSelected = currentNode == null,
                    OnSelect = () =>
                    {
                        ApplyTerminalNodeSelection(targetObject, questGraphPropertyPath, questNodePropertyPath, null);
                    }
                }
            };

            entries.AddRange(terminalNodes.Select(nodeData => new QuestCardSelectorPopup.Entry
            {
                Title = QuestPreviewUtility.GetNodeDisplayName(nodeData),
                Subtitle = nodeData.OwnerGraph != null ? QuestPreviewUtility.GetQuestDisplayName(nodeData.OwnerGraph) : "Unknown Quest",
                Sprite = nodeData.Icon,
                IsSelected = nodeData == currentNode,
                OnSelect = () =>
                {
                    ApplyTerminalNodeSelection(targetObject, questGraphPropertyPath, questNodePropertyPath, nodeData);
                }
            }));

            QuestCardSelectorPopup.Show(activatorRect, "Select Terminal Node", entries);
        }

        private void ApplySourceNodeSelection(
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            string questSourceNodePropertyPath,
            string questTransitionPropertyPath,
            QuestNodeData selectedNode)
        {
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty questGraphProperty = serializedObject.FindProperty(questGraphPropertyPath);
            SerializedProperty questSourceNodeProperty = serializedObject.FindProperty(questSourceNodePropertyPath);
            SerializedProperty questTransitionProperty = serializedObject.FindProperty(questTransitionPropertyPath);

            questGraphProperty.objectReferenceValue = selectedNode != null ? selectedNode.OwnerGraph : null;
            questSourceNodeProperty.objectReferenceValue = selectedNode;
            questTransitionProperty.objectReferenceValue = null;

            serializedObject.ApplyModifiedProperties();
            MarkDirty(targetObject);
            Repaint();
        }

        private void ApplyQuestGraphSelection(
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            QuestGraph selectedQuestGraph)
        {
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty questGraphProperty = serializedObject.FindProperty(questGraphPropertyPath);
            questGraphProperty.objectReferenceValue = selectedQuestGraph;
            serializedObject.ApplyModifiedProperties();
            MarkDirty(targetObject);
            Repaint();
        }

        private void ApplyTransitionSelection(
            UnityEngine.Object targetObject,
            string questTransitionPropertyPath,
            QuestTransition transition)
        {
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty questTransitionProperty = serializedObject.FindProperty(questTransitionPropertyPath);
            questTransitionProperty.objectReferenceValue = transition;
            serializedObject.ApplyModifiedProperties();
            MarkDirty(targetObject);
            Repaint();
        }

        private void ApplyTerminalNodeSelection(
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            string questNodePropertyPath,
            QuestNodeData selectedNode)
        {
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty questGraphProperty = serializedObject.FindProperty(questGraphPropertyPath);
            SerializedProperty questNodeProperty = serializedObject.FindProperty(questNodePropertyPath);

            questGraphProperty.objectReferenceValue = selectedNode != null ? selectedNode.OwnerGraph : null;
            questNodeProperty.objectReferenceValue = selectedNode;

            serializedObject.ApplyModifiedProperties();
            MarkDirty(targetObject);
            Repaint();
        }

        private static List<QuestNodeData> GetAllQuestSourceNodes()
        {
            cachedQuestSourceNodes ??= AssetDatabase.FindAssets("t:QuestNodeData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestNodeData>)
                .Where(nodeData =>
                    nodeData != null &&
                    nodeData.OwnerGraph != null &&
                    nodeData.Transitions != null &&
                    nodeData.Transitions.Any(transition => transition != null))
                .ToList();

            return cachedQuestSourceNodes;
        }

        private static List<QuestGraph> GetAllQuestGraphs()
        {
            cachedQuestGraphs ??= AssetDatabase.FindAssets("t:QuestGraph")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestGraph>)
                .Where(questGraph => questGraph != null)
                .ToList();

            return cachedQuestGraphs;
        }

        private static List<QuestNodeData> GetAllTerminalQuestNodes()
        {
            cachedTerminalQuestNodes ??= AssetDatabase.FindAssets("t:QuestNodeData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestNodeData>)
                .Where(nodeData =>
                    nodeData != null &&
                    nodeData.OwnerGraph != null &&
                    nodeData.OwnerGraph.IsTerminalNode(nodeData))
                .ToList();

            return cachedTerminalQuestNodes;
        }

        private static string GetQuestNodeOptionLabel(QuestNodeData nodeData)
        {
            if (nodeData == null)
            {
                return "<None>";
            }

            string questName = nodeData.OwnerGraph != null
                ? QuestPreviewUtility.GetQuestDisplayName(nodeData.OwnerGraph)
                : "Unknown Quest";

            return $"{questName} / {QuestPreviewUtility.GetNodeDisplayName(nodeData)}";
        }

        private static string GetQuestTransitionLabel(QuestGraph questGraph, QuestNodeData sourceNodeData, QuestTransition transition)
        {
            if (questGraph == null || transition == null)
            {
                return "<None>";
            }

            QuestNodeData ownerNodeData = sourceNodeData ?? GetOwnerNodeDataForTransition(questGraph, transition);

            string sourceName = ownerNodeData != null
                ? QuestPreviewUtility.GetNodeDisplayName(ownerNodeData)
                : "Unknown";
            string targetName = transition.TargetNode != null
                ? QuestPreviewUtility.GetNodeDisplayName(transition.TargetNode)
                : "Missing Target";
            return $"{sourceName} -> {targetName}";
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

        private static void ApplyEntrySelection(
            SerializedProperty keyIdProperty,
            SerializedProperty keyProperty,
            IReadOnlyList<SharedTableData.SharedTableEntry> entries,
            int selectedEntryIndex)
        {
            if (selectedEntryIndex <= 0)
            {
                keyIdProperty.longValue = 0;
                keyProperty.stringValue = string.Empty;
                return;
            }

            SharedTableData.SharedTableEntry entry = entries[selectedEntryIndex - 1];
            keyIdProperty.longValue = entry.Id;
            keyProperty.stringValue = string.Empty;
        }

        private static void ApplyEntrySelection(
            SerializedProperty keyIdProperty,
            SerializedProperty keyProperty,
            IReadOnlyList<LocalizedEntrySelectorWindow.EntryOption> entries,
            int selectedEntryIndex)
        {
            if (selectedEntryIndex <= 0)
            {
                keyIdProperty.longValue = 0;
                keyProperty.stringValue = string.Empty;
                return;
            }

            LocalizedEntrySelectorWindow.EntryOption entry = entries[selectedEntryIndex - 1];
            keyIdProperty.longValue = entry.Id;
            keyProperty.stringValue = string.Empty;
        }

        private static void ApplyEntrySelectionToObject(
            UnityEngine.Object targetObject,
            string keyIdPropertyPath,
            string keyPropertyPath,
            IReadOnlyList<SharedTableData.SharedTableEntry> entries,
            int selectedEntryIndex)
        {
            if (targetObject == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty keyIdProperty = serializedObject.FindProperty(keyIdPropertyPath);
            SerializedProperty keyProperty = serializedObject.FindProperty(keyPropertyPath);
            if (keyIdProperty == null || keyProperty == null)
            {
                return;
            }

            ApplyEntrySelection(keyIdProperty, keyProperty, entries, selectedEntryIndex);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
        }

        private static void ApplyEntrySelectionToObject(
            UnityEngine.Object targetObject,
            string keyIdPropertyPath,
            string keyPropertyPath,
            IReadOnlyList<LocalizedEntrySelectorWindow.EntryOption> entries,
            int selectedEntryIndex)
        {
            if (targetObject == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty keyIdProperty = serializedObject.FindProperty(keyIdPropertyPath);
            SerializedProperty keyProperty = serializedObject.FindProperty(keyPropertyPath);
            if (keyIdProperty == null || keyProperty == null)
            {
                return;
            }

            ApplyEntrySelection(keyIdProperty, keyProperty, entries, selectedEntryIndex);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
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

        private void DeleteNode(DialogNode node, bool exitGui = true)
        {
            if (activeConnectionNode == node)
            {
                activeConnectionNode = null;
            }

            bool shouldDeletePhraseAsset = node.Phrase != null &&
                                           EditorUtility.DisplayDialog(
                                               "Delete Phrase?",
                                               $"Do you want to delete the phrase \"{node.Phrase.name}\" from the project?",
                                               "Yes",
                                               "No");

            if (node.Phrase != null)
            {
                RemovePhraseReferences(node.Phrase);

                if (shouldDeletePhraseAsset)
                {
                    string phrasePath = AssetDatabase.GetAssetPath(node.Phrase);
                    if (!string.IsNullOrEmpty(phrasePath))
                    {
                        AssetDatabase.DeleteAsset(phrasePath);
                    }
                }
            }

            currentGraph.Nodes.Remove(node);
            MarkDirty(currentGraph);
            InvalidatePhraseDisplayName(node.Phrase);
            InvalidateGraphStructure();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (exitGui)
            {
                GUIUtility.ExitGUI();
            }
        }

        private void RemovePhraseReferences(DialogPhrase phrase)
        {
            if (currentGraph.IsEntryPhrase(phrase))
            {
                currentGraph.SetEntryPhrase(null);
            }

            foreach (DialogNode otherNode in currentGraph.Nodes)
            {
                if (otherNode.Phrase == null)
                {
                    continue;
                }

                bool phraseChanged = false;
                foreach (DialogAnswer answer in otherNode.Phrase.Answers)
                {
                    if (answer != null && answer.NextPhrase == phrase)
                    {
                        answer.SetNextPhrase(null);
                        phraseChanged = true;
                    }
                }

                if (phraseChanged)
                {
                    MarkDirty(otherNode.Phrase);
                    InvalidatePhraseDisplayName(otherNode.Phrase);
                }
            }

            if (sourcePhraseForSelection == phrase || pendingAnswer != null && pendingAnswer.NextPhrase == phrase)
            {
                CancelTargetSelection();
            }
        }

        private void ReplacePhraseReferences(DialogPhrase oldPhrase, DialogPhrase newPhrase)
        {
            if (oldPhrase == null || oldPhrase == newPhrase)
            {
                return;
            }

            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node.Phrase == null)
                {
                    continue;
                }

                bool phraseChanged = false;
                foreach (DialogAnswer answer in node.Phrase.Answers)
                {
                    if (answer != null && answer.NextPhrase == oldPhrase)
                    {
                        answer.SetNextPhrase(newPhrase);
                        phraseChanged = true;
                    }
                }

                if (phraseChanged)
                {
                    MarkDirty(node.Phrase);
                    InvalidatePhraseDisplayName(node.Phrase);
                }
            }

            InvalidateGraphCaches();
        }

        private bool ContainsPhrase(DialogPhrase phrase)
        {
            return phrase != null && phraseToNodeLookup.ContainsKey(phrase);
        }

        private bool IsOrphanPhrase(DialogPhrase phrase)
        {
            return phrase != null && orphanPhrases.Contains(phrase);
        }

        private Color GetNodeTint(DialogNode node)
        {
            if (node.Phrase == null)
            {
                return Color.white;
            }

            if (currentGraph.IsEntryPhrase(node.Phrase))
            {
                return StartNodeTint;
            }

            if (node.Phrase.IsQuestPhrase)
            {
                return QuestNodeTint;
            }

            if (IsOrphanPhrase(node.Phrase))
            {
                return OrphanNodeTint;
            }

            return Color.white;
        }

        private string GetNodeTitle(DialogNode node)
        {
            if (node.Phrase == null)
            {
                return "Phrase Node";
            }

            string prefix = string.Empty;
            if (currentGraph.IsEntryPhrase(node.Phrase))
            {
                prefix += "[Start] ";
            }

            if (node.Phrase.IsQuestPhrase)
            {
                prefix += "[Quest] ";
            }

            return prefix + GetCachedPhraseDisplayName(node.Phrase);
        }

        private string GetCachedPhraseDisplayName(DialogPhrase phrase)
        {
            if (phrase == null)
            {
                return "Phrase Node";
            }

            if (!phraseDisplayNameCache.TryGetValue(phrase, out string displayName))
            {
                displayName = GetPhraseDisplayName(phrase);
                phraseDisplayNameCache[phrase] = displayName;
            }

            return displayName;
        }

        private void InvalidatePhraseDisplayName(DialogPhrase phrase)
        {
            if (phrase != null)
            {
                phraseDisplayNameCache.Remove(phrase);
            }
        }

        private string GetPhraseDisplayName(DialogPhrase phrase)
        {
            if (phrase == null)
            {
                return "Phrase Node";
            }

            if (phraseDisplayNameCache.TryGetValue(phrase, out string cachedDisplayName))
            {
                return cachedDisplayName;
            }

            SerializedObject phraseObject = new SerializedObject(phrase);
            SerializedProperty textProperty = phraseObject.FindProperty("text");
            SerializedProperty tableReferenceProperty = textProperty?.FindPropertyRelative("m_TableReference");
            SerializedProperty tableCollectionNameProperty = tableReferenceProperty?.FindPropertyRelative("m_TableCollectionName");
            SerializedProperty entryReferenceProperty = textProperty?.FindPropertyRelative("m_TableEntryReference");
            SerializedProperty keyProperty = entryReferenceProperty?.FindPropertyRelative("m_Key");
            SerializedProperty keyIdProperty = entryReferenceProperty?.FindPropertyRelative("m_KeyId");

            if (tableCollectionNameProperty == null || string.IsNullOrWhiteSpace(tableCollectionNameProperty.stringValue))
            {
                return "\u041d\u0435\u0442 \u0441\u0442\u0440\u043e\u043a\u0438: " + phrase.name;
            }

            if ((keyProperty == null || string.IsNullOrWhiteSpace(keyProperty.stringValue)) &&
                (keyIdProperty == null || keyIdProperty.longValue == 0))
            {
                return "\u041d\u0435\u0442 \u0441\u0442\u0440\u043e\u043a\u0438: " + phrase.name;
            }

            if (keyProperty != null && !string.IsNullOrWhiteSpace(keyProperty.stringValue))
            {
                return keyProperty.stringValue;
            }

            if (keyIdProperty != null && keyIdProperty.longValue != 0)
            {
                return $"Key {keyIdProperty.longValue}";
            }

            return $"Нет строки: {phrase.name}";
        }

        private void CancelTargetSelection(bool repaint = true)
        {
            isSelectingTargetPhrase = false;
            pendingAnswer = null;
            sourcePhraseForSelection = null;
            toolkitCanvas?.RefreshTargetSelection();

            if (repaint)
            {
                Repaint();
            }
        }

        private void MarkDirty(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            InvalidateGraphCaches();
        }

        private string GetThemeToggleLabel()
        {
            return useLightTheme ? "Switch to Night Theme" : "Switch to Light Theme";
        }

        private void ApplyThemeGuiColors()
        {
            if (!useLightTheme)
            {
                return;
            }

            GUI.backgroundColor = ControlBackgroundColor;
            GUI.contentColor = ControlContentColor;
        }

        private void ApplyThemeSkin()
        {
            if (!useLightTheme)
            {
                return;
            }

            GUI.skin = LightSkin;
        }

        private void DrawWindowBackground()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), WindowBackgroundColor);
        }

        private void ApplyThemeEditorStyleTextOverrides()
        {
            if (!useLightTheme || Event.current.type != EventType.Repaint)
            {
                return;
            }

            editorStyleTextOverrides.Clear();
            OverrideEditorStyleTextColor(EditorStyles.label);
            OverrideEditorStyleTextColor(EditorStyles.boldLabel);
            OverrideEditorStyleTextColor(EditorStyles.miniLabel);
            OverrideEditorStyleTextColor(EditorStyles.miniBoldLabel);
            OverrideEditorStyleTextColor(EditorStyles.wordWrappedLabel);
            OverrideEditorStyleTextColor(EditorStyles.wordWrappedMiniLabel);
            OverrideEditorStyleTextColor(EditorStyles.centeredGreyMiniLabel);
            OverrideEditorStyleTextColor(EditorStyles.foldout);
            OverrideEditorStyleTextColor(EditorStyles.toggle);
            OverrideEditorStyleTextColor(EditorStyles.textField);
            OverrideEditorStyleTextColor(EditorStyles.textArea);
            OverrideEditorStyleTextColor(EditorStyles.popup);
            OverrideEditorStyleTextColor(EditorStyles.miniButton);
            OverrideEditorStyleTextColor(EditorStyles.miniButtonLeft);
            OverrideEditorStyleTextColor(EditorStyles.miniButtonMid);
            OverrideEditorStyleTextColor(EditorStyles.miniButtonRight);
            OverrideEditorStyleTextColor(EditorStyles.objectField);
            OverrideEditorStyleTextColor(EditorStyles.objectFieldThumb);
            OverrideEditorStyleTextColor(EditorStyles.helpBox);
        }

        private void RestoreThemeEditorStyleTextOverrides()
        {
            if (editorStyleTextOverrides.Count == 0)
            {
                return;
            }

            for (int i = editorStyleTextOverrides.Count - 1; i >= 0; i--)
            {
                editorStyleTextOverrides[i].Restore();
            }

            editorStyleTextOverrides.Clear();
        }

        private void OverrideEditorStyleTextColor(GUIStyle style)
        {
            if (style == null)
            {
                return;
            }

            editorStyleTextOverrides.Add(new EditorStyleTextOverride(style));
            SetStyleTextColor(style, Color.black);
        }

        private bool DrawButton(Rect rect, string label)
        {
            return GUI.Button(rect, label, ButtonStyle);
        }

        private bool DrawMiniButton(Rect rect, string label)
        {
            return GUI.Button(rect, label, MiniButtonStyle);
        }

        private bool DrawButton(string label, params GUILayoutOption[] options)
        {
            return GUILayout.Button(label, ButtonStyle, options);
        }

        private bool DrawMiniButton(string label, params GUILayoutOption[] options)
        {
            return GUILayout.Button(label, MiniButtonStyle, options);
        }

        private GUIStyle NodeWindowStyle => useLightTheme
            ? lightWindowStyle ??= CreateNodeWindowStyle()
            : GUI.skin.window;

        private GUIStyle HelpBoxStyle => useLightTheme
            ? lightHelpBoxStyle ??= CreateHelpBoxStyle()
            : EditorStyles.helpBox;

        private GUIStyle ButtonStyle => useLightTheme
            ? lightButtonStyle ??= CreateButtonStyle(GUI.skin.button)
            : GUI.skin.button;

        private GUIStyle MiniButtonStyle => useLightTheme
            ? lightMiniButtonStyle ??= CreateButtonStyle(EditorStyles.miniButton)
            : EditorStyles.miniButton;

        private GUIStyle PopupStyle => useLightTheme
            ? lightPopupStyle ??= CreatePopupStyle()
            : EditorStyles.popup;

        private GUIStyle TextFieldStyle => useLightTheme
            ? lightTextFieldStyle ??= CreateTextInputStyle(EditorStyles.textField)
            : EditorStyles.textField;

        private GUIStyle LabelStyle => useLightTheme
            ? lightLabelStyle ??= CreateLabelStyle(EditorStyles.label)
            : EditorStyles.label;

        private GUIStyle FoldoutStyle => useLightTheme
            ? lightFoldoutStyle ??= CreateLabelStyle(EditorStyles.foldout)
            : EditorStyles.foldout;

        private GUIStyle BoldLabelStyle => useLightTheme
            ? lightBoldLabelStyle ??= CreateLabelStyle(EditorStyles.boldLabel)
            : EditorStyles.boldLabel;

        private GUIStyle MiniBoldLabelStyle => useLightTheme
            ? lightMiniBoldLabelStyle ??= CreateLabelStyle(EditorStyles.miniBoldLabel)
            : EditorStyles.miniBoldLabel;

        private GUIStyle MiniLabelStyle => useLightTheme
            ? lightMiniLabelStyle ??= CreateLabelStyle(EditorStyles.miniLabel, MutedContentColor)
            : EditorStyles.miniLabel;

        private GUIStyle WordWrappedMiniLabelStyle => useLightTheme
            ? lightWordWrappedMiniLabelStyle ??= CreateLabelStyle(EditorStyles.wordWrappedMiniLabel, MutedContentColor)
            : EditorStyles.wordWrappedMiniLabel;

        private GUIStyle CenteredMiniLabelStyle => useLightTheme
            ? lightCenteredMiniLabelStyle ??= CreateLabelStyle(EditorStyles.centeredGreyMiniLabel, MutedContentColor)
            : EditorStyles.centeredGreyMiniLabel;

        private GUIStyle PreviewLabelStyle => useLightTheme
            ? lightPreviewLabelStyle ??= CreatePreviewTextStyle()
            : EditorStyles.wordWrappedLabel;

        private GUISkin LightSkin => lightSkin ??= CreateLightSkin();

        private GUIStyle CreateNodeWindowStyle()
        {
            lightWindowTexture ??= CreateSolidTexture(new Color(0.95f, 0.96f, 0.98f, 1f));

            var style = new GUIStyle(GUI.skin.window);
            ApplyThemeState(style.normal, lightWindowTexture, ControlContentColor);
            ApplyThemeState(style.hover, lightWindowTexture, ControlContentColor);
            ApplyThemeState(style.active, lightWindowTexture, ControlContentColor);
            ApplyThemeState(style.focused, lightWindowTexture, ControlContentColor);
            ApplyThemeState(style.onNormal, lightWindowTexture, ControlContentColor);
            ApplyThemeState(style.onHover, lightWindowTexture, ControlContentColor);
            ApplyThemeState(style.onActive, lightWindowTexture, ControlContentColor);
            ApplyThemeState(style.onFocused, lightWindowTexture, ControlContentColor);

            return style;
        }

        private GUIStyle CreateHelpBoxStyle()
        {
            lightHelpBoxTexture ??= CreateSolidTexture(new Color(0.96f, 0.95f, 0.92f, 1f));

            var style = new GUIStyle(EditorStyles.helpBox);
            ApplyThemeState(style.normal, lightHelpBoxTexture, ControlContentColor);
            ApplyThemeState(style.hover, lightHelpBoxTexture, ControlContentColor);
            ApplyThemeState(style.active, lightHelpBoxTexture, ControlContentColor);
            ApplyThemeState(style.focused, lightHelpBoxTexture, ControlContentColor);

            return style;
        }

        private GUIStyle CreateButtonStyle(GUIStyle sourceStyle)
        {
            lightButtonTexture ??= CreateSolidTexture(new Color(0.94f, 0.92f, 0.88f, 1f));
            lightButtonHoverTexture ??= CreateSolidTexture(new Color(0.91f, 0.89f, 0.85f, 1f));
            lightButtonActiveTexture ??= CreateSolidTexture(new Color(0.87f, 0.85f, 0.81f, 1f));

            var style = new GUIStyle(sourceStyle);
            ApplyThemeState(style.normal, lightButtonTexture, ControlContentColor);
            ApplyThemeState(style.hover, lightButtonHoverTexture, ControlContentColor);
            ApplyThemeState(style.active, lightButtonActiveTexture, ControlContentColor);
            ApplyThemeState(style.focused, lightButtonHoverTexture, ControlContentColor);
            ApplyThemeState(style.onNormal, lightButtonTexture, ControlContentColor);
            ApplyThemeState(style.onHover, lightButtonHoverTexture, ControlContentColor);
            ApplyThemeState(style.onActive, lightButtonActiveTexture, ControlContentColor);
            ApplyThemeState(style.onFocused, lightButtonHoverTexture, ControlContentColor);

            return style;
        }

        private GUIStyle CreateTextInputStyle(GUIStyle sourceStyle)
        {
            lightTextFieldTexture ??= CreateSolidTexture(new Color(0.98f, 0.97f, 0.95f, 1f));

            var style = new GUIStyle(sourceStyle);
            ApplyThemeState(style.normal, lightTextFieldTexture, ControlContentColor);
            ApplyThemeState(style.hover, lightTextFieldTexture, ControlContentColor);
            ApplyThemeState(style.active, lightTextFieldTexture, ControlContentColor);
            ApplyThemeState(style.focused, lightTextFieldTexture, ControlContentColor);
            ApplyThemeState(style.onNormal, lightTextFieldTexture, ControlContentColor);
            ApplyThemeState(style.onHover, lightTextFieldTexture, ControlContentColor);
            ApplyThemeState(style.onActive, lightTextFieldTexture, ControlContentColor);
            ApplyThemeState(style.onFocused, lightTextFieldTexture, ControlContentColor);

            return style;
        }

        private GUIStyle CreatePopupStyle()
        {
            var style = CreateButtonStyle(EditorStyles.popup);
            style.alignment = TextAnchor.MiddleLeft;
            return style;
        }

        private GUIStyle CreatePreviewTextStyle()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(6, 6, 4, 4)
            };

            style.normal.textColor = ControlContentColor;
            style.hover.textColor = ControlContentColor;
            style.active.textColor = ControlContentColor;
            style.focused.textColor = ControlContentColor;
            return style;
        }

        private GUISkin CreateLightSkin()
        {
            GUISkin sourceSkin = GUI.skin;
            GUISkin skin = UnityEngine.Object.Instantiate(sourceSkin);
            skin.label = CreateLabelStyle(sourceSkin.label);
            skin.button = CreateButtonStyle(sourceSkin.button);
            skin.textField = CreateTextInputStyle(sourceSkin.textField);
            skin.textArea = CreateTextInputStyle(sourceSkin.textArea);
            skin.box = CreateHelpBoxStyle();
            skin.window = CreateNodeWindowStyle();
            skin.toggle = CreateLabelStyle(sourceSkin.toggle);
            skin.settings.selectionColor = new Color(0.77f, 0.84f, 0.93f, 1f);
            skin.settings.cursorColor = ControlContentColor;

            skin.customStyles = RegisterLightCustomStyles(sourceSkin, skin.customStyles);

            return skin;
        }

        private GUIStyle[] RegisterLightCustomStyles(GUISkin sourceSkin, GUIStyle[] styles)
        {
            styles = RegisterNamedStyle(sourceSkin, styles, "TextField", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "TextArea", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "IN TextField", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "ObjectField", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "ObjectFieldButton", lightButtonTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "IN ObjectField", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "IN ObjectFieldText", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "Popup", lightButtonTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "IN Popup", lightButtonTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "MiniPopup", lightButtonTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "MiniPullDown", lightButtonTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "DropDown", lightButtonTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "DropDownButton", lightButtonTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "ObjectFieldThumb", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "ObjectFieldMiniThumb", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "SearchTextField", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "ToolbarSearchTextField", lightTextFieldTexture);
            styles = RegisterNamedStyle(sourceSkin, styles, "ToolbarSeachTextField", lightTextFieldTexture);
            return styles;
        }

        private GUIStyle[] RegisterNamedStyle(GUISkin sourceSkin, GUIStyle[] styles, string styleName, Texture2D backgroundTexture)
        {
            GUIStyle style = sourceSkin.FindStyle(styleName);
            return style != null
                ? AppendOrReplaceStyle(styles, CreateNamedStyle(style, styleName, backgroundTexture))
                : styles;
        }

        private GUIStyle CreateLabelStyle(GUIStyle sourceStyle)
        {
            return CreateLabelStyle(sourceStyle, ControlContentColor);
        }

        private GUIStyle CreateLabelStyle(GUIStyle sourceStyle, Color textColor)
        {
            var style = new GUIStyle(sourceStyle);
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.onFocused.textColor = textColor;
            return style;
        }

        private GUIStyle CreateNamedStyle(GUIStyle sourceStyle, string styleName, Texture2D backgroundTexture)
        {
            var style = new GUIStyle(sourceStyle) { name = styleName };
            ApplyThemeState(style.normal, backgroundTexture, ControlContentColor);
            ApplyThemeState(style.hover, backgroundTexture, ControlContentColor);
            ApplyThemeState(style.active, backgroundTexture, ControlContentColor);
            ApplyThemeState(style.focused, backgroundTexture, ControlContentColor);
            ApplyThemeState(style.onNormal, backgroundTexture, ControlContentColor);
            ApplyThemeState(style.onHover, backgroundTexture, ControlContentColor);
            ApplyThemeState(style.onActive, backgroundTexture, ControlContentColor);
            ApplyThemeState(style.onFocused, backgroundTexture, ControlContentColor);
            return style;
        }

        private void DrawEnumPropertyField(SerializedProperty property, string label)
        {
            if (property == null)
            {
                return;
            }

            if (property.propertyType != SerializedPropertyType.Enum)
            {
                DrawPropertyFieldWithCustomLabel(property, label);
                return;
            }

            int selectedIndex = DrawPopupField(label, property.enumValueIndex, property.enumDisplayNames);
            if (selectedIndex != property.enumValueIndex)
            {
                property.enumValueIndex = selectedIndex;
            }
        }

        private void DrawPropertyFieldWithCustomLabel(SerializedProperty property, string label, bool includeChildren = false)
        {
            if (property == null)
            {
                return;
            }

            float height = EditorGUI.GetPropertyHeight(property, includeChildren);
            Rect totalRect = EditorGUILayout.GetControlRect(true, height);
            Rect fieldRect = EditorGUI.PrefixLabel(totalRect, new GUIContent(label), LabelStyle);
            List<EditorStyleTextOverride> temporaryOverrides = null;

            if (useLightTheme &&
                Event.current.type == EventType.Repaint &&
                property.propertyType == SerializedPropertyType.ObjectReference)
            {
                temporaryOverrides = CreateTemporaryObjectFieldTextOverrides(Color.white);
            }

            try
            {
                EditorGUI.PropertyField(fieldRect, property, GUIContent.none, includeChildren);
            }
            finally
            {
                RestoreTemporaryStyleTextOverrides(temporaryOverrides);
            }
        }

        private int DrawPopupField(string label, int selectedIndex, string[] options)
        {
            Rect totalRect = EditorGUILayout.GetControlRect();
            Rect fieldRect = EditorGUI.PrefixLabel(totalRect, new GUIContent(label), LabelStyle);
            return EditorGUI.Popup(fieldRect, selectedIndex, options, PopupStyle);
        }

        private static void ApplyThemeState(GUIStyleState state, Texture2D backgroundTexture, Color textColor)
        {
            state.background = backgroundTexture;
            state.scaledBackgrounds = new[] { backgroundTexture };
            state.textColor = textColor;
        }

        private static void SetStyleTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private List<EditorStyleTextOverride> CreateTemporaryObjectFieldTextOverrides(Color textColor)
        {
            var overrides = new List<EditorStyleTextOverride>(7);
            AddTemporaryStyleTextOverride(overrides, EditorStyles.objectField, textColor);
            AddTemporaryStyleTextOverride(overrides, EditorStyles.objectFieldThumb, textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "ObjectField", textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "ObjectFieldButton", textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "ObjectFieldThumb", textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "IN ObjectField", textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "IN ObjectFieldText", textColor);
            return overrides;
        }

        private static void AddTemporarySkinStyleTextOverride(
            List<EditorStyleTextOverride> overrides,
            GUISkin skin,
            string styleName,
            Color textColor)
        {
            if (skin == null)
            {
                return;
            }

            AddTemporaryStyleTextOverride(overrides, skin.FindStyle(styleName), textColor);
        }

        private static void AddTemporaryStyleTextOverride(
            List<EditorStyleTextOverride> overrides,
            GUIStyle style,
            Color textColor)
        {
            if (style == null)
            {
                return;
            }

            overrides.Add(new EditorStyleTextOverride(style));
            SetStyleTextColor(style, textColor);
        }

        private static void RestoreTemporaryStyleTextOverrides(List<EditorStyleTextOverride> overrides)
        {
            if (overrides == null)
            {
                return;
            }

            for (int i = overrides.Count - 1; i >= 0; i--)
            {
                overrides[i].Restore();
            }
        }

        private static GUIStyle[] AppendOrReplaceStyle(GUIStyle[] styles, GUIStyle style)
        {
            if (styles == null || styles.Length == 0)
            {
                return new[] { style };
            }

            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] != null && styles[i].name == style.name)
                {
                    styles[i] = style;
                    return styles;
                }
            }

            GUIStyle[] result = new GUIStyle[styles.Length + 1];
            styles.CopyTo(result, 0);
            result[styles.Length] = style;
            return result;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private readonly struct EditorStyleTextOverride
        {
            private readonly GUIStyle style;
            private readonly Color normal;
            private readonly Color hover;
            private readonly Color active;
            private readonly Color focused;
            private readonly Color onNormal;
            private readonly Color onHover;
            private readonly Color onActive;
            private readonly Color onFocused;

            public EditorStyleTextOverride(GUIStyle style)
            {
                this.style = style;
                normal = style.normal.textColor;
                hover = style.hover.textColor;
                active = style.active.textColor;
                focused = style.focused.textColor;
                onNormal = style.onNormal.textColor;
                onHover = style.onHover.textColor;
                onActive = style.onActive.textColor;
                onFocused = style.onFocused.textColor;
            }

            public void Restore()
            {
                style.normal.textColor = normal;
                style.hover.textColor = hover;
                style.active.textColor = active;
                style.focused.textColor = focused;
                style.onNormal.textColor = onNormal;
                style.onHover.textColor = onHover;
                style.onActive.textColor = onActive;
                style.onFocused.textColor = onFocused;
            }
        }

        private Color PanelBackgroundColor => useLightTheme
            ? new Color(0.96f, 0.95f, 0.92f, 1f)
            : new Color(0.18f, 0.18f, 0.18f, 1f);

        private Color CanvasBackgroundColor => useLightTheme
            ? new Color(0.98f, 0.97f, 0.95f, 1f)
            : new Color(0.13f, 0.13f, 0.13f, 1f);

        private Color MinorGridColor => useLightTheme
            ? new Color(0.35f, 0.40f, 0.48f, 0.18f)
            : new Color(0.25f, 0.25f, 0.25f, 0.35f);

        private Color MajorGridColor => useLightTheme
            ? new Color(0.32f, 0.37f, 0.46f, 0.30f)
            : new Color(0.25f, 0.25f, 0.25f, 0.60f);

        private Color PrimaryConnectionColor => useLightTheme
            ? new Color(0.30f, 0.28f, 0.24f, 0.98f)
            : new Color(0.96f, 0.96f, 0.96f, 0.98f);

        private Color SourceHighlightConnectionColor => useLightTheme
            ? new Color(0.18f, 0.18f, 0.18f, 0.98f)
            : new Color(1f, 1f, 1f, 0.98f);

        private Color TargetHighlightConnectionColor => useLightTheme
            ? new Color(0.86f, 0.18f, 0.18f, 0.98f)
            : new Color(1f, 0.28f, 0.28f, 0.98f);

        private Color ControlBackgroundColor => useLightTheme
            ? new Color(0.96f, 0.95f, 0.92f, 1f)
            : Color.white;

        private Color ControlContentColor => useLightTheme
            ? Color.black
            : Color.white;

        private Color MutedContentColor => useLightTheme
            ? Color.black
            : new Color(0.75f, 0.75f, 0.75f, 1f);

        private Color WindowBackgroundColor => useLightTheme
            ? new Color(0.97f, 0.96f, 0.94f, 1f)
            : new Color(0.22f, 0.22f, 0.22f, 1f);

        private Color DangerButtonColor => useLightTheme
            ? new Color(0.88f, 0.32f, 0.32f, 1f)
            : new Color(1f, 0.40f, 0.40f, 1f);

        private Color LinkButtonColor => useLightTheme
            ? new Color(0.84f, 0.62f, 0.18f, 1f)
            : new Color(1f, 0.70f, 0.20f, 1f);

        private Color StartBadgeColor => useLightTheme
            ? new Color(0.22f, 0.60f, 0.26f, 1f)
            : new Color(0.20f, 0.70f, 0.25f, 1f);

        private Color WarningBadgeColor => useLightTheme
            ? new Color(0.84f, 0.56f, 0.14f, 1f)
            : new Color(1f, 0.60f, 0.15f, 1f);

        private Color StartNodeTint => useLightTheme
            ? new Color(0.84f, 0.95f, 0.84f, 1f)
            : new Color(0.82f, 1f, 0.82f, 1f);

        private Color QuestNodeTint => useLightTheme
            ? new Color(0.84f, 0.90f, 0.98f, 1f)
            : new Color(0.80f, 0.90f, 1f, 1f);

        private Color OrphanNodeTint => useLightTheme
            ? new Color(0.98f, 0.90f, 0.74f, 1f)
            : new Color(1f, 0.92f, 0.72f, 1f);

        private Color SelectionOverlayTextColor => useLightTheme
            ? new Color(0.10f, 0.16f, 0.12f, 1f)
            : Color.white;

        private Color DangerAccentColor => useLightTheme
            ? new Color(0.82f, 0.30f, 0.30f, 1f)
            : new Color(0.92f, 0.34f, 0.34f, 1f);

        private Color WarningAccentColor => useLightTheme
            ? new Color(0.78f, 0.52f, 0.12f, 1f)
            : new Color(0.95f, 0.66f, 0.22f, 1f);

        private Color ConditionAccentColor => useLightTheme
            ? new Color(0.21f, 0.52f, 0.72f, 1f)
            : new Color(0.26f, 0.63f, 0.86f, 1f);

        private Color RewardAccentColor => useLightTheme
            ? new Color(0.31f, 0.65f, 0.35f, 1f)
            : new Color(0.38f, 0.78f, 0.42f, 1f);

        private Color ItemAccentColor => useLightTheme
            ? new Color(0.20f, 0.66f, 0.64f, 1f)
            : new Color(0.24f, 0.78f, 0.76f, 1f);

        private Color StrongDividerColor => useLightTheme
            ? new Color(0f, 0f, 0f, 0.12f)
            : new Color(1f, 1f, 1f, 0.12f);

        private Color SectionDividerColor => useLightTheme
            ? new Color(0f, 0f, 0f, 0.10f)
            : new Color(1f, 1f, 1f, 0.10f);

        private Color SoftDividerColor => useLightTheme
            ? new Color(0f, 0f, 0f, 0.08f)
            : new Color(1f, 1f, 1f, 0.08f);

        private Color SoftestDividerColor => useLightTheme
            ? new Color(0f, 0f, 0f, 0.06f)
            : new Color(1f, 1f, 1f, 0.06f);

        private Color GetSelectionOverlayColor(bool isHovered)
        {
            return useLightTheme
                ? new Color(0.18f, 0.65f, 0.24f, isHovered ? 0.32f : 0.18f)
                : new Color(0f, 0.75f, 0.20f, isHovered ? 0.45f : 0.25f);
        }
    }
}
