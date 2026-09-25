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
using UnityEngine.UIElements;

namespace Dialogs.Graph.Editor
{
    public partial class DialogEditorWindow : GraphEditorWindowBase
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
        private readonly Dictionary<DialogNode, Rect> nodeRects = new();
        private readonly GraphEditorNodeLayoutSynchronizer<DialogNode> nodeLayoutSynchronizer = new();
        private readonly DialogGraphIndex graphIndex = new();
        private readonly DialogPhraseDisplayNameCache phraseDisplayNames = new();
        private readonly DialogExitAbilityCache exitAbilityCache = new();
        private readonly HashSet<DialogPhrase> phrasesWithDirtyLayout = new();
        private readonly HashSet<DialogPhrase> phrasesAwaitingRepaintAfterLayout = new();
        private readonly GUIContent localizedPreviewContent = new();
        private readonly DialogAnswerFoldoutState answerFoldoutState = new();
        private bool graphStructureDirty = true;
        private readonly Dictionary<DialogAnswer, CachedConnectionRoute> connectionRouteCache = new();
        private readonly Dictionary<ImplicitConnectionRouteKey, CachedConnectionRoute> implicitConnectionRouteCache = new();
        private readonly List<Rect> connectionObstacleRects = new();
        private int connectionLayoutVersion;

        private static System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> cachedStringTableCollections;
        private static string[] cachedStringTableOptions;
        private static readonly Dictionary<string, CachedLocalizedEntryOptions> localizedEntryOptionsCache = new();
        private static List<QuestGraph> cachedQuestGraphs;
        private static List<QuestNodeData> cachedQuestSourceNodes;
        private static List<QuestNodeData> cachedTerminalQuestNodes;

        private readonly DialogTargetSelectionState targetSelection = new();
        private bool isControlsPanelExpanded = true;
        private DialogNode activeConnectionNode;
        private readonly GraphEditorStyleTextOverrides styleTextOverrides = new();
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
            toolkitCanvas = new DialogToolkitCanvas(this);
            toolkitControls = GraphEditorWindowUi.BuildRoot(
                rootVisualElement,
                toolkitCanvas,
                "dialog-editor-controls",
                OverlayPanelWidth,
                WindowBackgroundColor,
                DrawToolkitControls);

            toolkitCanvas.RebuildNow();
        }

        private void OnEnable()
        {
            dialogsFolderPath = GraphEditorPreferences.LoadFolder(DialogsPathKey, "Assets/Dialogs");
            phrasesFolderPath = GraphEditorPreferences.LoadFolder(PhrasesPathKey, "Assets/DialogPhrases");
            useLightTheme = GraphEditorPreferences.LoadTheme(ThemeKey);
            GraphEditorAssetChangeTracker.AssetsChanged += HandleTrackedAssetsChanged;
        }

        private void OnDisable()
        {
            GraphEditorAssetChangeTracker.AssetsChanged -= HandleTrackedAssetsChanged;
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

            using (BeginThemedGuiScope())
            {
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
        }

        private void DrawToolkitControls()
        {
            using (BeginThemedGuiScope())
            {
                DrawControlsOverlay();
            }
        }

        private void DrawToolkitNode(DialogNode node)
        {
            using (BeginThemedGuiScope())
            {
                DrawNodeWindow(node, false);
            }
        }

        private void RefreshToolkitCanvas(bool rebuild = false)
        {
            GraphEditorWindowUi.Refresh(
                rootVisualElement,
                toolkitCanvas,
                toolkitControls,
                WindowBackgroundColor,
                rebuild);
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
                GraphEditorPreferences.SaveFolder(DialogsPathKey, dialogsFolderPath);
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
                GraphEditorPreferences.SaveFolder(PhrasesPathKey, phrasesFolderPath);
            }

            y += 36f;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), GetThemeToggleLabel()))
            {
                useLightTheme = !useLightTheme;
                GraphEditorPreferences.SaveTheme(ThemeKey, useLightTheme);
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

            if (targetSelection.IsActive)
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
            if (!GraphEditorAssetService.TryPickAssetsFolder(title, folderPath, out string selectedPath))
            {
                return;
            }

            folderPath = selectedPath;
            GraphEditorPreferences.SaveFolder(prefsKey, folderPath);
        }

        private void CreateNewGraph()
        {
            if (!EnsureFolderExists(dialogsFolderPath, "Please specify the folder for saving dialogs."))
            {
                return;
            }

            currentGraph = CreateInstance<DialogGraph>();
            GraphEditorAssetService.CreateAsset(currentGraph, dialogsFolderPath, "DialogGraph.asset", "Create dialog graph");
            InvalidateGraphStructure();
        }

        private void LoadGraph()
        {
            if (!GraphEditorAssetService.TryLoadAsset("Load Dialog Graph", out currentGraph))
            {
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
            var phrase = CreateInstance<DialogPhrase>();
            GraphEditorAssetService.CreateAsset(phrase, phrasesFolderPath, fileName, "Create dialog phrase");

            var newNode = new DialogNode(phrase)
            {
                Position = GetCenteredNodePosition(new Vector2(DialogNodeWidth, 220f))
            };

            GraphEditorAssetService.MarkDirty(currentGraph, "Add dialog phrase");
            currentGraph.Nodes.Add(newNode);
            MarkDirty(currentGraph);
            InvalidateGraphStructure();

        }

        private bool EnsureFolderExists(string folderPath, string emptyPathMessage)
        {
            return GraphEditorAssetService.EnsureFolderExists(folderPath, emptyPathMessage);
        }

        private void DrawGraphArea()
        {
            Event currentEvent = Event.current;
            if (graphStructureDirty)
            {
                CleanupGraph();
                graphStructureDirty = false;
            }

            if (graphIndex.IsDirty)
            {
                RebuildGraphCaches();
            }

            SynchronizeNodeRects();
            HandleZoom(currentEvent, ZoomMin, ZoomMax, WorkspaceWidth, WorkspaceHeight);
            HandlePan(currentEvent, WorkspaceWidth, WorkspaceHeight);
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
            if (nodeLayoutSynchronizer.Synchronize(
                    currentGraph.Nodes,
                    nodeRects,
                    node => node.Position,
                    new Vector2(DialogNodeWidth, 220f)))
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
            graphIndex.Rebuild(currentGraph);
            InvalidateConnectionRouteCache();
        }

        private void InvalidateGraphCaches()
        {
            graphIndex.Invalidate();
            phraseDisplayNames.Clear();
            exitAbilityCache.Clear();
            phrasesWithDirtyLayout.Clear();
            phrasesAwaitingRepaintAfterLayout.Clear();
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
            implicitConnectionRouteCache.Clear();
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

        private void HandleTrackedAssetsChanged(IReadOnlyCollection<string> changedAssetPaths)
        {
            if (!HasRelevantAssetChange(changedAssetPaths))
            {
                return;
            }

            InvalidateStaticEditorCaches();
            bool graphAssetChanged = currentGraph != null &&
                changedAssetPaths.Contains(AssetDatabase.GetAssetPath(currentGraph));
            if (graphAssetChanged || HasMissingPhraseReference())
            {
                InvalidateGraphStructure();
                return;
            }

            InvalidateGraphCaches();
        }

        private bool HasRelevantAssetChange(IReadOnlyCollection<string> changedAssetPaths)
        {
            return GraphEditorAssetChangeFilter.HasRelevantChange(
                changedAssetPaths,
                currentGraph != null ? AssetDatabase.GetAssetPath(currentGraph) : null,
                dialogsFolderPath,
                phrasesFolderPath);
        }

        private bool HasMissingPhraseReference()
        {
            return currentGraph != null && currentGraph.Nodes.Any(node =>
                node == null || node.Phrase == null || !AssetDatabase.Contains(node.Phrase));
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

                foreach (DialogAnswer answer in GetConnectionAnswers(phrase))
                {
                    DrawConnection(node, answer, false);
                }

                foreach (DialogPhrase returnAction in GetImplicitConversationReturnActions(phrase))
                {
                    if (graphIndex.TryGetNode(returnAction, out DialogNode targetNode))
                    {
                        DrawConnection(node, null, targetNode, true);
                    }
                }
            }

            Handles.EndGUI();
        }

        private void DrawConnection(DialogNode sourceNode, DialogAnswer answer, bool isImplicit)
        {
            if (answer?.NextPhrase == null || !graphIndex.TryGetNode(answer.NextPhrase, out DialogNode targetNode))
            {
                return;
            }

            DrawConnection(sourceNode, answer, targetNode, isImplicit);
        }

        private void DrawConnection(DialogNode sourceNode, DialogAnswer answer, DialogNode targetNode, bool isImplicit)
        {
            if (!nodeRects.TryGetValue(sourceNode, out Rect sourceRect) ||
                !nodeRects.TryGetValue(targetNode, out Rect targetRect))
            {
                return;
            }

            (Vector2 startPos, Vector2 endPos) = DialogConnectionRouter.GetConnectionAnchors(sourceRect, targetRect);
            (Vector2 startTangent, Vector2 endTangent) = GetOrBuildConnectionTangents(
                answer,
                startPos,
                endPos,
                sourceRect,
                targetRect,
                sourceNode,
                targetNode);

            Color connectionColor = GetConnectionColor(sourceNode, targetNode);
            if (isImplicit)
            {
                connectionColor = Color.Lerp(connectionColor, new Color(0.76f, 0.82f, 0.94f), 0.6f);
            }

            Handles.DrawBezier(startPos, endPos, startTangent, endTangent, connectionColor, null, 3f);
            DialogConnectionRouter.DrawConnectionArrow(endPos, endPos - endTangent);
        }

        private static IEnumerable<DialogAnswer> GetConnectionAnswers(DialogPhrase phrase)
        {
            if (phrase == null)
            {
                yield break;
            }

            foreach (DialogAnswer answer in phrase.Answers)
            {
                yield return answer;
            }

            if (phrase.IsQuestPhrase)
            {
                yield return phrase.QuestAnswer;
            }

            if (phrase.IsConversationTopic)
            {
                yield return phrase.ConversationAnswer;
            }

            if (phrase.IsConversationReturnAction)
            {
                yield return phrase.ConversationReturnAnswer;
            }

            if (phrase.IsDialogueExitAction)
            {
                yield return phrase.DialogueExitAnswer;
            }
        }

        private IEnumerable<DialogPhrase> GetImplicitConversationReturnActions(DialogPhrase phrase)
        {
            if (currentGraph == null || phrase == null)
            {
                yield break;
            }

            foreach (DialogPhrase returnAction in currentGraph.GetConversationReturnPhrases(phrase))
            {
                yield return returnAction;
            }
        }

        private void HandleConnectionHighlightSelection(Event currentEvent)
        {
            if (targetSelection.IsActive ||
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
            ImplicitConnectionRouteKey implicitKey = new(sourceNode, targetNode);
            bool hasCachedRoute = answer != null
                ? connectionRouteCache.TryGetValue(answer, out CachedConnectionRoute cachedRoute)
                : implicitConnectionRouteCache.TryGetValue(implicitKey, out cachedRoute);
            if (hasCachedRoute &&
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
                    connectionObstacleRects.Add(DialogConnectionRouter.ExpandRect(pair.Value, 8f));
                }
            }

            (Vector2 startTangent, Vector2 endTangent) = DialogConnectionRouter.ResolveConnectionTangents(
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
            else
            {
                implicitConnectionRouteCache[implicitKey] = new CachedConnectionRoute(
                    connectionLayoutVersion,
                    startPos,
                    sourceRect,
                    targetRect,
                    startTangent,
                    endTangent);
            }

            return (startTangent, endTangent);
        }


        private static bool ApproximatelyEqual(Vector2 a, Vector2 b)
        {
            return GraphConnectionGeometry.ApproximatelyEqual(a, b);
        }

        private static bool RectApproximatelyEqual(Rect a, Rect b)
        {
            return GraphConnectionGeometry.ApproximatelyEqual(a, b);
        }


        private void DrawTargetSelectionOverlay(Rect visibleGraphRect)
        {
            if (!targetSelection.IsActive || targetSelection.PendingAnswer == null)
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
                    targetSelection.PendingAnswer.SetNextPhrase(node.Phrase);
                    MarkDirty(targetSelection.SourcePhrase);
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
                    GraphEditorAssetService.DeleteAsset(node.Phrase, "Delete dialog phrase");
                }
            }

            GraphEditorAssetService.MarkDirty(currentGraph, "Delete dialog node");
            currentGraph.Nodes.Remove(node);
            MarkDirty(currentGraph);
            InvalidatePhraseDisplayName(node.Phrase);
            InvalidateGraphStructure();
            GraphEditorAssetService.FlushChanges();
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

            if (targetSelection.SourcePhrase == phrase || targetSelection.PendingAnswer != null && targetSelection.PendingAnswer.NextPhrase == phrase)
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

                foreach (DialogAnswer navigationAnswer in new[]
                         {
                             node.Phrase.QuestAnswer,
                             node.Phrase.ConversationAnswer,
                             node.Phrase.ConversationReturnAnswer,
                             node.Phrase.DialogueExitAnswer
                         })
                {
                    if (navigationAnswer != null && navigationAnswer.NextPhrase == oldPhrase)
                    {
                        navigationAnswer.SetNextPhrase(newPhrase);
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
            return graphIndex.Contains(phrase);
        }

        private bool IsOrphanPhrase(DialogPhrase phrase)
        {
            return graphIndex.IsOrphan(phrase);
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

            if (node.Phrase.IsConversationTopic)
            {
                return new Color(0.72f, 0.86f, 0.76f);
            }

            if (node.Phrase.IsConversationReturnAction)
            {
                return new Color(0.76f, 0.82f, 0.94f);
            }

            if (node.Phrase.IsDialogueExitAction)
            {
                return new Color(0.94f, 0.76f, 0.76f);
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

            if (node.Phrase.IsConversationTopic)
            {
                prefix += "[Topic] ";
            }

            if (node.Phrase.IsConversationReturnAction)
            {
                prefix += "[Return] ";
            }

            if (node.Phrase.IsDialogueExitAction)
            {
                prefix += "[Exit] ";
            }

            return prefix + GetCachedPhraseDisplayName(node.Phrase);
        }

        private string GetCachedPhraseDisplayName(DialogPhrase phrase)
        {
            return phraseDisplayNames.Get(phrase);
        }

        private void InvalidatePhraseDisplayName(DialogPhrase phrase)
        {
            if (phrase != null)
            {
                phraseDisplayNames.Invalidate(phrase);
            }
        }

        private string GetPhraseDisplayName(DialogPhrase phrase)
        {
            return phraseDisplayNames.Get(phrase);

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
            targetSelection.Cancel();
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

        private void MarkNodePositionDirty()
        {
            if (currentGraph != null)
            {
                EditorUtility.SetDirty(currentGraph);
            }
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
            if (!useLightTheme)
            {
                return;
            }
            styleTextOverrides.ApplyForLightTheme();
        }

        private void RestoreThemeEditorStyleTextOverrides()
        {
            styleTextOverrides.Restore();
        }

        protected override void ApplyThemedGuiState()
        {
            ApplyThemeGuiColors();
            ApplyThemeSkin();
            ApplyThemeEditorStyleTextOverrides();
        }

        protected override void RestoreThemedGuiState()
        {
            RestoreThemeEditorStyleTextOverrides();
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
            GraphEditorGuiStyleUtility.ApplyState(state, backgroundTexture, textColor);
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
            return GraphEditorGuiStyleUtility.AppendOrReplace(styles, style);
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            return GraphEditorGuiStyleUtility.CreateSolidTexture(color);
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
