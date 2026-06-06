using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Quests.Editor;
using Quests.MapTargets;
using Quests.Graph.Model;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Quests.Graph.Editor
{
    public class QuestEditorWindow : EditorWindow
    {
        private const string PreferredPreviewLocale = "ru";
        private const string NodesPathKey = "QuestEditor_NodesPath";
        private const string TransitionsPathKey = "QuestEditor_TransitionsPath";
        private const string ThemeKey = "QuestEditor_Theme";
        private const float LocalizedPreviewMinHeight = 48f;
        private const float LocalizedPreviewWidth = 268f;
        private const float WorkspaceWidth = 10000f;
        private const float WorkspaceHeight = 10000f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 2f;
        private const float OverlayPanelWidth = 320f;
        private const float AccentLineWidth = 1.5f;
        private static readonly Vector2 NodeSize = new(360f, 420f);

        private QuestGraph currentGraph;
        private Vector2 scrollPos;
        private string nodesFolderPath;
        private string transitionsFolderPath;
        private readonly Dictionary<QuestTransition, Vector2> transitionAnchorPositions = new();
        private readonly Dictionary<QuestNode, Rect> nodeRects = new();
        private readonly Dictionary<string, bool> transitionFoldoutStates = new();
        private static System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> cachedStringTableCollections;
        private static string[] cachedStringTableOptions;
        private static readonly Dictionary<string, CachedLocalizedEntryOptions> localizedEntryOptionsCache = new();

        private bool isSelectingTargetNode;
        private bool isControlsPanelExpanded = true;
        private QuestTransition pendingTransition;
        private QuestNode sourceNodeForSelection;
        private QuestNode activeConnectionNode;
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

        [MenuItem("Tools/Quest Editor")]
        public static void Open()
        {
            GetWindow<QuestEditorWindow>("Quest Editor");
        }

        private void OnEnable()
        {
            nodesFolderPath = EditorPrefs.GetString(NodesPathKey, "Assets/QuestNodes");
            transitionsFolderPath = EditorPrefs.GetString(TransitionsPathKey, "Assets/QuestTransitions");
            useLightTheme = EditorPrefs.GetBool(ThemeKey, false);
            EditorApplication.projectChanged += HandleProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
        }

        private void OnGUI()
        {
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

                EnsureGraphNodes();
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

        private void DrawEmptyState()
        {
            Rect contentRect = new Rect(12f, 12f, position.width - 24f, 52f);
            EditorGUI.DrawRect(contentRect, PanelBackgroundColor);
            GUI.Box(contentRect, GUIContent.none, HelpBoxStyle);
            EditorGUI.LabelField(
                new Rect(contentRect.x + 10f, contentRect.y + 10f, contentRect.width - 20f, 32f),
                "Create or load a quest graph.");
        }

        private void DrawControlsOverlay()
        {
            const float toggleButtonWidth = 24f;
            const float toggleButtonHeight = 64f;
            const float spacing = 6f;
            const float padding = 10f;
            const float collapsedToggleLeftOffset = 6f;
            const float buttonHeight = 28f;
            float panelHeight = Mathf.Max(120f, position.height);
            float panelY = position.height - panelHeight;
            Rect panelRect = new Rect(0f, panelY, OverlayPanelWidth, panelHeight);

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

            float y = padding;

            EditorGUI.DrawRect(panelRect, PanelBackgroundColor);
            GUI.Box(panelRect, GUIContent.none, HelpBoxStyle);
            GUILayout.BeginArea(panelRect, GUIContent.none, HelpBoxStyle);
            float contentWidth = OverlayPanelWidth - padding * 2f;

            EditorGUI.LabelField(new Rect(padding, padding, contentWidth, 18f), "Quest Nodes Folder Path:");
            y += 18f;

            nodesFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), nodesFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Quest Nodes", ref nodesFolderPath, NodesPathKey);
            }

            if (DrawButton(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                EditorPrefs.SetString(NodesPathKey, nodesFolderPath);
            }

            y += 28f;

            EditorGUI.LabelField(new Rect(padding, y, contentWidth, 18f), "Transitions Folder Path:");
            y += 18f;

            transitionsFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), transitionsFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Quest Transitions", ref transitionsFolderPath, TransitionsPathKey);
            }

            if (DrawButton(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                EditorPrefs.SetString(TransitionsPathKey, transitionsFolderPath);
            }

            y += 36f;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), GetThemeToggleLabel()))
            {
                useLightTheme = !useLightTheme;
                EditorPrefs.SetBool(ThemeKey, useLightTheme);
                Repaint();
            }

            y += buttonHeight + spacing;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "New Graph"))
            {
                CreateNewGraph();
            }

            y += buttonHeight + spacing;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Load Graph"))
            {
                LoadGraph();
            }

            y += buttonHeight + spacing;

            EditorGUI.BeginDisabledGroup(currentGraph == null);
            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "New Node"))
            {
                CreateNewNode();
            }
            EditorGUI.EndDisabledGroup();

            y += buttonHeight + spacing;

            if (currentGraph != null)
            {
                QuestNodeData startNode = GetStartNode();
                if (startNode == null)
                {
                    EditorGUI.HelpBox(
                        new Rect(padding, y, contentWidth, 40f),
                        "Start node is not defined. The quest graph will not work without it.",
                        MessageType.Warning);
                    y += 46f;
                }

                EditorGUI.BeginDisabledGroup(startNode == null);
                if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Ping Start Node"))
                {
                    EditorGUIUtility.PingObject(startNode);
                    Selection.activeObject = startNode;
                }
                EditorGUI.EndDisabledGroup();
                y += buttonHeight + spacing;

                GUILayout.BeginArea(new Rect(padding, y, contentWidth, 112f));
                QuestPreviewUtility.DrawQuestGraphPreview(currentGraph, "Quest");
                GUILayout.EndArea();
                y += 118f;

                SerializedObject graphObject = new SerializedObject(currentGraph);
                graphObject.Update();
                SerializedProperty titleProperty = graphObject.FindProperty("title");
                SerializedProperty descriptionProperty = graphObject.FindProperty("description");
                if (titleProperty != null)
                {
                    y = DrawLocalizedFieldArea(padding, y, contentWidth, titleProperty, "Name");
                }

                if (descriptionProperty != null)
                {
                    y = DrawLocalizedFieldArea(padding, y, contentWidth, descriptionProperty, "Description");
                }

                if (graphObject.hasModifiedProperties)
                {
                    graphObject.ApplyModifiedProperties();
                    MarkDirty(currentGraph);
                }
            }

            if (isSelectingTargetNode)
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
            currentGraph = CreateInstance<QuestGraph>();
            ProjectWindowUtil.CreateAsset(currentGraph, "NewQuestGraph.asset");
        }

        private void LoadGraph()
        {
            string path = EditorUtility.OpenFilePanel("Load Quest Graph", "Assets", "asset");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = "Assets" + path.Replace(Application.dataPath, "");
            currentGraph = AssetDatabase.LoadAssetAtPath<QuestGraph>(path);
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog("Invalid Asset", "Selected asset is not a QuestGraph.", "OK");
                return;
            }

            EnsureGraphNodes();
            ClaimUnownedNodes();
        }

        private void CreateNewNode()
        {
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog(
                    "No Graph Selected",
                    "Please create or load a quest graph first.",
                    "OK");
                return;
            }

            EnsureGraphNodes();

            if (!EnsureFolderExists(nodesFolderPath, "Please specify the folder for saving quest nodes."))
            {
                return;
            }

            string fileName = $"QuestNode_{currentGraph.Nodes.Count}.asset";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(nodesFolderPath, fileName));

            var nodeData = CreateInstance<QuestNodeData>();
            string defaultNodeTitle = Path.GetFileNameWithoutExtension(targetPath);
            nodeData.name = defaultNodeTitle;
            nodeData.SetEditorTitle(defaultNodeTitle);
            nodeData.SetOwnerGraph(currentGraph);

            AssetDatabase.CreateAsset(nodeData, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var newNode = new QuestNode(nodeData)
            {
                Position = GetCenteredNodePosition(NodeSize)
            };

            currentGraph.Nodes.Add(newNode);
            MarkDirty(currentGraph);
            MarkDirty(nodeData);

            EditorGUIUtility.PingObject(nodeData);
            Selection.activeObject = nodeData;
        }

        private static bool EnsureFolderExists(string folderPath, string emptyPathMessage)
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
            CleanupGraph();
            transitionAnchorPositions.Clear();
            nodeRects.Clear();

            Event currentEvent = Event.current;
            HandleZoom(currentEvent);
            HandlePan(currentEvent);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            GUI.EndClip();
            GUI.EndClip();
            GUI.BeginClip(new Rect(Vector2.zero, new Vector2(WorkspaceWidth, WorkspaceHeight)));
            GUI.BeginClip(new Rect(Vector2.zero, new Vector2(WorkspaceWidth, WorkspaceHeight)));

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(panOffset, Quaternion.identity, Vector3.one * zoom);

            if (currentEvent.type == EventType.Repaint)
            {
                DrawBackgroundGrid(new Rect(0f, 0f, WorkspaceWidth, WorkspaceHeight));
            }

            BeginWindows();

            for (int i = 0; i < currentGraph.Nodes.Count; i++)
            {
                QuestNode node = currentGraph.Nodes[i];
                Rect rect = new Rect(node.Position, NodeSize);

                Color previousColor = GUI.color;
                GUI.color = GetNodeTint(node);
                rect = GUILayout.Window(i, rect, _ => DrawNodeWindow(node), GetNodeTitle(node), NodeWindowStyle);
                GUI.color = previousColor;

                nodeRects[node] = rect;
                node.Position = rect.position;
            }

            EndWindows();
            HandleConnectionHighlightSelection(currentEvent);

            if (currentEvent.type == EventType.Repaint)
            {
                DrawNodeMarkers();
                DrawConnections();
            }

            DrawTargetSelectionOverlay();

            GUI.matrix = oldMatrix;
            EditorGUILayout.EndScrollView();
        }

        private void CleanupGraph()
        {
            if (currentGraph == null)
            {
                return;
            }

            EnsureGraphNodes();
            bool graphChanged = false;

            for (int i = currentGraph.Nodes.Count - 1; i >= 0; i--)
            {
                QuestNode node = currentGraph.Nodes[i];
                if (node == null)
                {
                    currentGraph.Nodes.RemoveAt(i);
                    graphChanged = true;
                    continue;
                }

                if (node.NodeData == null || !AssetDatabase.Contains(node.NodeData))
                {
                    currentGraph.Nodes.RemoveAt(i);
                    graphChanged = true;
                }
            }

            if (graphChanged)
            {
                MarkDirty(currentGraph);
            }
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

        private void DrawNodeMarkers()
        {
            foreach (QuestNode node in currentGraph.Nodes)
            {
                if (node.NodeData == null)
                {
                    continue;
                }

                Rect badgeRect = new Rect(node.Position.x + 6f, node.Position.y + 6f, 18f, 18f);
                Color previous = GUI.backgroundColor;

                if (IsStartNode(node))
                {
                    GUI.backgroundColor = StartBadgeColor;
                    GUI.Box(badgeRect, "S");
                }
                else if (IsOrphanNode(node.NodeData))
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

            foreach (KeyValuePair<QuestNode, Rect> pair in nodeRects)
            {
                QuestNode node = pair.Key;
                QuestNodeData nodeData = node.NodeData;
                if (nodeData == null)
                {
                    continue;
                }

                EnsureCollections(nodeData);

                foreach (QuestTransition transition in nodeData.Transitions)
                {
                    if (transition == null || transition.TargetNode == null)
                    {
                        continue;
                    }

                    QuestNode targetNode = currentGraph.Nodes.FirstOrDefault(n => n.NodeData == transition.TargetNode);
                    if (targetNode == null)
                    {
                        continue;
                    }

                    if (!nodeRects.TryGetValue(node, out Rect sourceRect) || !nodeRects.TryGetValue(targetNode, out Rect targetRect))
                    {
                        continue;
                    }

                    Vector2 startPos = transitionAnchorPositions.TryGetValue(transition, out Vector2 anchorPosition)
                        ? anchorPosition
                        : new Vector2(sourceRect.xMax - 12f, sourceRect.center.y);
                    Vector2 endPos = GetNearestSideCenter(targetRect, startPos);

                    Handles.color = GetConnectionColor(node, targetNode);
                    (Vector2 startTangent, Vector2 endTangent) = ResolveConnectionTangents(
                        startPos,
                        endPos,
                        sourceRect,
                        targetRect,
                        nodeRects
                            .Where(item => item.Key != node && item.Key != targetNode)
                            .Select(item => ExpandRect(item.Value, 8f))
                            .ToList());

                    Handles.DrawBezier(startPos, endPos, startTangent, endTangent, Handles.color, null, 4.5f);
                    DrawConnectionArrow(endPos, endPos - endTangent);
                }
            }

            Handles.EndGUI();
        }

        private void HandleConnectionHighlightSelection(Event currentEvent)
        {
            if (isSelectingTargetNode ||
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

        private Color GetConnectionColor(QuestNode sourceNode, QuestNode targetNode)
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

        private void DrawTargetSelectionOverlay()
        {
            if (!isSelectingTargetNode || pendingTransition == null)
            {
                return;
            }

            Handles.BeginGUI();
            Vector2 graphMousePosition = GetGraphMousePosition(Event.current.mousePosition);

            foreach (QuestNode node in currentGraph.Nodes)
            {
                if (node == sourceNodeForSelection || node.NodeData == null)
                {
                    continue;
                }

                if (!nodeRects.TryGetValue(node, out Rect rect))
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
                    pendingTransition.SetTargetNode(node.NodeData);
                    MarkDirty(pendingTransition);
                    CancelTargetSelection(false);
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            Handles.EndGUI();
        }

        private void DrawNodeWindow(QuestNode node)
        {
            if (TryHandleTargetNodeSelection(node))
            {
                return;
            }

            if (!isSelectingTargetNode &&
                Event.current.rawType == EventType.MouseDown &&
                Event.current.button == 0 &&
                activeConnectionNode != node)
            {
                activeConnectionNode = node;
                Repaint();
            }

            EditorGUI.BeginDisabledGroup(isSelectingTargetNode);

            Rect removeButtonRect = new Rect(298f, 5f, 16f, 16f);
            if (DrawMiniButton(removeButtonRect, "x"))
            {
                DeleteNode(node);
                EditorGUI.EndDisabledGroup();
                return;
            }

            EditorGUI.BeginChangeCheck();
            var newNodeData = (QuestNodeData)EditorGUILayout.ObjectField(node.NodeData, typeof(QuestNodeData), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (newNodeData != null && currentGraph.Nodes.Exists(n => n != node && n.NodeData == newNodeData))
                {
                    EditorUtility.DisplayDialog(
                        "Duplicate Node Detected",
                        $"Node \"{newNodeData.name}\" is already assigned to another graph node.",
                        "OK");
                }
                else if (!CanAssignNodeToCurrentGraph(newNodeData, node.NodeData))
                {
                    EditorUtility.DisplayDialog(
                        "Node Already Belongs To Another Quest",
                        $"Node \"{newNodeData.name}\" already belongs to quest \"{newNodeData.OwnerGraph.name}\".",
                        "OK");
                }
                else
                {
                    QuestNodeData oldNodeData = node.NodeData;
                    ReplaceNodeReferences(oldNodeData, newNodeData);
                    node.NodeData = newNodeData;
                    AssignNodeToCurrentGraph(newNodeData);
                    ReleaseNodeOwnershipIfUnused(oldNodeData);
                    MarkDirty(currentGraph);
                }
            }

            if (node.NodeData == null)
            {
                EditorGUILayout.HelpBox("No quest node asset assigned.", MessageType.Warning);
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                EditorGUI.EndDisabledGroup();
                return;
            }

            EnsureCollections(node.NodeData);

            if (node.NodeData.OwnerGraph != null && node.NodeData.OwnerGraph != currentGraph)
            {
                EditorGUILayout.HelpBox(
                    $"This node belongs to quest \"{node.NodeData.OwnerGraph.name}\" and cannot be edited here.",
                    MessageType.Error);
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                EditorGUI.EndDisabledGroup();
                return;
            }

            DrawNodeDataEditor(node);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Transitions", MiniBoldLabelStyle);
            DrawTransitionsSection(node.NodeData, node);

            if (DrawButton(IsStartNode(node) ? "Start Node" : "Set As Start"))
            {
                MoveNodeToFront(node);
            }

            if (IsOrphanNode(node.NodeData))
            {
                EditorGUILayout.HelpBox(
                    "This node has no incoming transitions and is not the start node.",
                    MessageType.Warning);
            }

            EditorGUI.EndDisabledGroup();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private bool TryHandleTargetNodeSelection(QuestNode node)
        {
            if (!isSelectingTargetNode || pendingTransition == null)
            {
                return false;
            }

            if (node == null || node == sourceNodeForSelection || node.NodeData == null)
            {
                return false;
            }

            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
            {
                return false;
            }

            pendingTransition.SetTargetNode(node.NodeData);
            MarkDirty(pendingTransition);
            CancelTargetSelection(false);
            Event.current.Use();
            GUIUtility.ExitGUI();
            return true;
        }

        private void DrawTransitionsSection(QuestNodeData nodeData, QuestNode ownerNode)
        {
            EnsureCollections(nodeData);
            CleanupMissingTransitions(nodeData);

            int removeTransitionIndex = -1;

            for (int i = 0; i < nodeData.Transitions.Count; i++)
            {
                QuestTransition transition = nodeData.Transitions[i];
                if (transition == null || !AssetDatabase.Contains(transition))
                {
                    continue;
                }

                bool missingLink = transition.TargetNode == null;
                bool targetOutsideGraph = transition.TargetNode != null && !ContainsNode(transition.TargetNode);
                int conditionCount = transition.Conditions?.Count ?? 0;
                int resultCount = transition.Results?.Count ?? 0;
                bool hasConditions = transition.HasConditions;
                bool hasResults = transition.HasResults;
                string foldoutKey = GetTransitionFoldoutKey(nodeData, i);
                bool isExpanded = GetTransitionFoldoutState(foldoutKey);
                Color accentColor = GetTransitionAccentColor(missingLink, targetOutsideGraph, hasConditions, hasResults);
                string statusLabel = GetTransitionStatusLabel(missingLink, targetOutsideGraph, hasConditions, conditionCount, hasResults, resultCount);

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
                bool newExpanded = EditorGUILayout.Foldout(isExpanded, $"Transition {i + 1}", true, FoldoutStyle);
                if (newExpanded != isExpanded)
                {
                    SetTransitionFoldoutState(foldoutKey, newExpanded);
                    isExpanded = newExpanded;
                }

                GUILayout.Label(statusLabel, CenteredMiniLabelStyle, GUILayout.Width(110f));
                GUILayout.FlexibleSpace();

                if (DrawMiniButton("X", GUILayout.Width(22f)))
                {
                    removeTransitionIndex = i;
                }

                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = missingLink ? DangerButtonColor : LinkButtonColor;
                bool pickPressed = DrawMiniButton("O", GUILayout.Width(22f));
                GUI.backgroundColor = previousBackground;

                Rect localButtonRect = GUILayoutUtility.GetLastRect();
                if (nodeRects.TryGetValue(ownerNode, out Rect nodeRect))
                {
                    Vector2 localCenter = new Vector2(
                        localButtonRect.x + localButtonRect.width * 0.5f,
                        localButtonRect.y + localButtonRect.height * 0.5f);
                    transitionAnchorPositions[transition] = nodeRect.position + localCenter;
                }

                if (pickPressed)
                {
                    isSelectingTargetNode = true;
                    pendingTransition = transition;
                    sourceNodeForSelection = ownerNode;
                }

                if (isExpanded)
                {
                    EditorGUILayout.EndHorizontal();
                    DrawQuestEditorDivider(StrongDividerColor);
                    EditorGUILayout.Space(3f);

                    DrawTransitionAssetField(nodeData, i);

                    transition = nodeData.Transitions[i];
                    if (transition != null)
                    {
                        DrawTransitionDataEditor(transition);

                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField("Target", transition.TargetNode, typeof(QuestNodeData), false);
                        EditorGUI.EndDisabledGroup();

                        if (transition.TargetNode == null)
                        {
                            EditorGUILayout.HelpBox("Target node is not assigned for this transition.", MessageType.Error);
                        }
                        else if (!ContainsNode(transition.TargetNode))
                        {
                            EditorGUILayout.HelpBox("Target node is not added to the current graph.", MessageType.Warning);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (i < nodeData.Transitions.Count - 1)
                {
                    EditorGUILayout.Space(3f);
                    DrawQuestEditorDivider(SoftDividerColor);
                    EditorGUILayout.Space(5f);
                }
                else
                {
                    EditorGUILayout.Space(4f);
                }
            }

            if (DrawButton("+ Add Transition"))
            {
                CreateTransitionAsset(nodeData);
                ClearTransitionFoldoutStates(nodeData);
            }

            if (removeTransitionIndex >= 0 && removeTransitionIndex < nodeData.Transitions.Count)
            {
                RemoveTransition(nodeData, removeTransitionIndex);
                ClearTransitionFoldoutStates(nodeData);
            }
        }

        private void DrawTransitionAssetField(QuestNodeData nodeData, int index)
        {
            QuestTransition transition = nodeData.Transitions[index];

            EditorGUI.BeginChangeCheck();
            var newTransition = (QuestTransition)EditorGUILayout.ObjectField("Asset", transition, typeof(QuestTransition), false);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            if (newTransition == null)
            {
                nodeData.Transitions[index] = null;
                MarkDirty(nodeData);
                return;
            }

            bool duplicateInOtherNode = currentGraph.Nodes.Any(node =>
                node.NodeData != null &&
                node.NodeData != nodeData &&
                node.NodeData.Transitions != null &&
                node.NodeData.Transitions.Contains(newTransition));

            bool duplicateInCurrentNode = nodeData.Transitions.Where((_, i) => i != index).Any(item => item == newTransition);

            if (duplicateInOtherNode || duplicateInCurrentNode)
            {
                EditorUtility.DisplayDialog(
                    "Duplicate Transition Detected",
                    $"Transition \"{newTransition.name}\" is already used in this graph.",
                    "OK");
                return;
            }

            nodeData.Transitions[index] = newTransition;
            MarkDirty(nodeData);
        }

        private void CleanupMissingTransitions(QuestNodeData nodeData)
        {
            for (int i = nodeData.Transitions.Count - 1; i >= 0; i--)
            {
                QuestTransition transition = nodeData.Transitions[i];
                if (transition == null || !AssetDatabase.Contains(transition))
                {
                    nodeData.Transitions.RemoveAt(i);
                    MarkDirty(nodeData);
                }
            }
        }

        private void CreateTransitionAsset(QuestNodeData nodeData)
        {
            if (!EnsureFolderExists(transitionsFolderPath, "Please specify the folder for saving quest transitions."))
            {
                return;
            }

            string fileName = $"{nodeData.name}_Transition_{nodeData.Transitions.Count}.asset";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(transitionsFolderPath, fileName));

            var newTransition = CreateInstance<QuestTransition>();
            newTransition.name = Path.GetFileNameWithoutExtension(targetPath);

            AssetDatabase.CreateAsset(newTransition, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            nodeData.Transitions.Add(newTransition);
            MarkDirty(nodeData);
        }

        private void RemoveTransition(QuestNodeData nodeData, int removeTransitionIndex)
        {
            QuestTransition removedTransition = nodeData.Transitions[removeTransitionIndex];
            nodeData.Transitions.RemoveAt(removeTransitionIndex);
            MarkDirty(nodeData);

            if (removedTransition != null)
            {
                string path = AssetDatabase.GetAssetPath(removedTransition);
                if (!string.IsNullOrEmpty(path))
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "Delete Transition?",
                        $"Do you want to delete the transition \"{removedTransition.name}\" from the project?",
                        "Yes",
                        "No");

                    if (confirm)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = null;
            GUIUtility.ExitGUI();
        }

        private void DeleteNode(QuestNode node)
        {
            bool shouldDeleteNodeAsset = node.NodeData != null &&
                                         EditorUtility.DisplayDialog(
                                             "Delete Node?",
                                             $"Do you want to delete the node \"{node.NodeData.name}\" from the project?",
                                             "Yes",
                                             "No");

            if (node.NodeData != null)
            {
                RemoveNodeReferences(node.NodeData);

                if (shouldDeleteNodeAsset)
                {
                    DeleteOwnedTransitions(node.NodeData);

                    string nodePath = AssetDatabase.GetAssetPath(node.NodeData);
                    if (!string.IsNullOrEmpty(nodePath))
                    {
                        AssetDatabase.DeleteAsset(nodePath);
                    }
                }
            }

            currentGraph.Nodes.Remove(node);
            if (!shouldDeleteNodeAsset)
            {
                ReleaseNodeOwnershipIfUnused(node.NodeData);
            }

            MarkDirty(currentGraph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
        }

        private void RemoveNodeReferences(QuestNodeData nodeData)
        {
            foreach (QuestNode otherNode in currentGraph.Nodes)
            {
                if (otherNode.NodeData == null)
                {
                    continue;
                }

                EnsureCollections(otherNode.NodeData);

                foreach (QuestTransition transition in otherNode.NodeData.Transitions)
                {
                    if (transition != null && transition.TargetNode == nodeData)
                    {
                        transition.SetTargetNode(null);
                        MarkDirty(transition);
                    }
                }
            }

            if ((sourceNodeForSelection != null && sourceNodeForSelection.NodeData == nodeData) ||
                (pendingTransition != null && pendingTransition.TargetNode == nodeData))
            {
                CancelTargetSelection();
            }
        }

        private void ReplaceNodeReferences(QuestNodeData oldNode, QuestNodeData newNode)
        {
            if (oldNode == null || oldNode == newNode)
            {
                return;
            }

            foreach (QuestNode node in currentGraph.Nodes)
            {
                if (node.NodeData == null)
                {
                    continue;
                }

                EnsureCollections(node.NodeData);

                foreach (QuestTransition transition in node.NodeData.Transitions)
                {
                    if (transition != null && transition.TargetNode == oldNode)
                    {
                        transition.SetTargetNode(newNode);
                        MarkDirty(transition);
                    }
                }
            }
        }

        private void DeleteOwnedTransitions(QuestNodeData nodeData)
        {
            EnsureCollections(nodeData);

            foreach (QuestTransition transition in nodeData.Transitions.ToList())
            {
                if (transition == null)
                {
                    continue;
                }

                string transitionPath = AssetDatabase.GetAssetPath(transition);
                if (!string.IsNullOrEmpty(transitionPath))
                {
                    AssetDatabase.DeleteAsset(transitionPath);
                }
            }

            nodeData.Transitions.Clear();
            MarkDirty(nodeData);
        }

        private void EnsureGraphNodes()
        {
            if (currentGraph == null)
            {
                return;
            }

            if (currentGraph.Nodes == null)
            {
                currentGraph.Nodes = new List<QuestNode>();
                MarkDirty(currentGraph);
            }
        }

        private QuestNodeData GetStartNode()
        {
            return currentGraph != null &&
                   currentGraph.Nodes.Count > 0 &&
                   currentGraph.Nodes[0] != null
                ? currentGraph.Nodes[0].NodeData
                : null;
        }

        private bool ContainsNode(QuestNodeData nodeData)
        {
            return currentGraph.Nodes.Any(node => node.NodeData == nodeData);
        }

        private bool IsStartNode(QuestNode node)
        {
            return currentGraph != null &&
                   currentGraph.Nodes.Count > 0 &&
                   currentGraph.Nodes[0] == node &&
                   node.NodeData != null;
        }

        private bool IsOrphanNode(QuestNodeData nodeData)
        {
            if (nodeData == null || GetStartNode() == nodeData)
            {
                return false;
            }

            return !currentGraph.Nodes
                .Where(node => node.NodeData != null)
                .SelectMany(node => node.NodeData.Transitions ?? new List<QuestTransition>())
                .Any(transition => transition != null && transition.TargetNode == nodeData);
        }

        private void MoveNodeToFront(QuestNode node)
        {
            int index = currentGraph.Nodes.IndexOf(node);
            if (index <= 0)
            {
                return;
            }

            currentGraph.Nodes.RemoveAt(index);
            currentGraph.Nodes.Insert(0, node);
            MarkDirty(currentGraph);
        }

        private Color GetNodeTint(QuestNode node)
        {
            if (node.NodeData == null)
            {
                return Color.white;
            }

            if (IsStartNode(node))
            {
                return StartNodeTint;
            }

            if (IsOrphanNode(node.NodeData))
            {
                return OrphanNodeTint;
            }

            return Color.white;
        }

        private void DrawNodeDataEditor(QuestNode node)
        {
            QuestNodeData nodeData = node.NodeData;
            SerializedObject nodeDataObject = new SerializedObject(nodeData);
            nodeDataObject.Update();

            SerializedProperty editorTitleProperty = nodeDataObject.FindProperty("editorTitle");
            SerializedProperty nameProperty = nodeDataObject.FindProperty("localizedName");
            SerializedProperty iconProperty = nodeDataObject.FindProperty("icon");
            SerializedProperty mapTargetSourceProperty = nodeDataObject.FindProperty("mapTargetSource");
            SerializedProperty sceneMapTargetIdProperty = nodeDataObject.FindProperty("sceneMapTargetId");
            SerializedProperty scriptMapTargetKeyProperty = nodeDataObject.FindProperty("scriptMapTargetKey");
            SerializedProperty hasAvailabilityRequirementsProperty = nodeDataObject.FindProperty("hasAvailabilityRequirements");
            SerializedProperty availabilityRequirementsProperty = nodeDataObject.FindProperty("availabilityRequirements");
            SerializedProperty hasCompletionResultsProperty = nodeDataObject.FindProperty("hasCompletionResults");
            SerializedProperty completionResultsProperty = nodeDataObject.FindProperty("completionResults");

            QuestPreviewUtility.DrawQuestNodePreview(nodeData, "Node");

            if (editorTitleProperty != null)
            {
                DrawPropertyFieldWithCustomLabel(editorTitleProperty, "Node Title");
            }

            if (nameProperty != null)
            {
                DrawLocalizedStringSelector(nameProperty, "Name");
            }

            if (iconProperty != null)
            {
                DrawPropertyFieldWithCustomLabel(iconProperty, "Sprite");
            }

            DrawMapTargetSelector(nodeData, mapTargetSourceProperty, sceneMapTargetIdProperty, scriptMapTargetKeyProperty);

            if (IsStartNode(node) && hasAvailabilityRequirementsProperty != null && availabilityRequirementsProperty != null)
            {
                EditorGUILayout.PropertyField(hasAvailabilityRequirementsProperty, new GUIContent("Has Unlock Conditions"));
                if (hasAvailabilityRequirementsProperty.boolValue)
                {
                    DrawQuestResourceEntries(availabilityRequirementsProperty, "Unlock Conditions");
                }
            }

            if (currentGraph.IsTerminalNode(nodeData) && hasCompletionResultsProperty != null && completionResultsProperty != null)
            {
                EditorGUILayout.PropertyField(hasCompletionResultsProperty, new GUIContent("Has Completion Rewards/Penalties"));
                if (hasCompletionResultsProperty.boolValue)
                {
                    DrawQuestResourceEntries(completionResultsProperty, "Completion Rewards/Penalties");
                }
            }

            if (nodeDataObject.hasModifiedProperties)
            {
                nodeDataObject.ApplyModifiedProperties();
                MarkDirty(nodeData);
            }
        }

        private void DrawMapTargetSelector(
            QuestNodeData nodeData,
            SerializedProperty mapTargetSourceProperty,
            SerializedProperty sceneMapTargetIdProperty,
            SerializedProperty scriptMapTargetKeyProperty)
        {
            if (nodeData == null ||
                mapTargetSourceProperty == null ||
                sceneMapTargetIdProperty == null ||
                scriptMapTargetKeyProperty == null)
            {
                return;
            }

            QuestGraph questGraph = nodeData.OwnerGraph != null ? nodeData.OwnerGraph : currentGraph;
            List<QuestMapTarget> availableTargets = GetAvailableMapTargets(questGraph);
            string[] options = BuildMapTargetOptions(availableTargets);
            int selectedIndex = GetSelectedMapTargetIndex(
                (QuestMapTargetSourceType)mapTargetSourceProperty.enumValueIndex,
                sceneMapTargetIdProperty.stringValue,
                availableTargets);

            int newSelectedIndex = DrawPopupField("Map Pointer", selectedIndex, options);
            if (newSelectedIndex != selectedIndex)
            {
                ApplyMapTargetSelection(
                    mapTargetSourceProperty,
                    sceneMapTargetIdProperty,
                    scriptMapTargetKeyProperty,
                    availableTargets,
                    newSelectedIndex);
            }

            QuestMapTargetSourceType sourceType = (QuestMapTargetSourceType)mapTargetSourceProperty.enumValueIndex;

            if (sourceType == QuestMapTargetSourceType.ScriptTarget)
            {
                DrawPropertyFieldWithCustomLabel(scriptMapTargetKeyProperty, "Script Target Key");
                if (string.IsNullOrWhiteSpace(scriptMapTargetKeyProperty.stringValue))
                {
                    EditorGUILayout.HelpBox("Script target mode requires a non-empty key.", MessageType.Warning);
                }
            }
            else if (sourceType == QuestMapTargetSourceType.SceneTarget)
            {
                QuestMapTarget selectedTarget = availableTargets.FirstOrDefault(target =>
                    target != null &&
                    target.TargetId == sceneMapTargetIdProperty.stringValue);

                if (selectedTarget == null)
                {
                    EditorGUILayout.HelpBox(
                        "Selected scene target is not available in the currently loaded scenes for this quest.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField("Scene Target", selectedTarget, typeof(QuestMapTarget), true);
                    EditorGUI.EndDisabledGroup();
                }
            }

            if (availableTargets.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No QuestMapTarget components are available for this quest in the loaded scenes.",
                    MessageType.Info);
            }
        }

        private static List<QuestMapTarget> GetAvailableMapTargets(QuestGraph questGraph)
        {
            if (questGraph == null)
            {
                return new List<QuestMapTarget>();
            }

            return Object.FindObjectsByType<QuestMapTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(target =>
                    target != null &&
                    target.QuestGraph == questGraph &&
                    target.gameObject.scene.IsValid() &&
                    target.gameObject.scene.isLoaded)
                .OrderBy(BuildMapTargetLabel)
                .ToList();
        }

        private static string[] BuildMapTargetOptions(IReadOnlyList<QuestMapTarget> availableTargets)
        {
            var options = new string[availableTargets.Count + 2];
            options[0] = "<None>";
            options[1] = "<Script Target>";

            for (int i = 0; i < availableTargets.Count; i++)
            {
                options[i + 2] = BuildMapTargetLabel(availableTargets[i]);
            }

            return options;
        }

        private static int GetSelectedMapTargetIndex(
            QuestMapTargetSourceType sourceType,
            string sceneMapTargetId,
            IReadOnlyList<QuestMapTarget> availableTargets)
        {
            if (sourceType == QuestMapTargetSourceType.ScriptTarget)
            {
                return 1;
            }

            if (sourceType == QuestMapTargetSourceType.SceneTarget)
            {
                for (int i = 0; i < availableTargets.Count; i++)
                {
                    if (availableTargets[i] != null && availableTargets[i].TargetId == sceneMapTargetId)
                    {
                        return i + 2;
                    }
                }
            }

            return 0;
        }

        private static void ApplyMapTargetSelection(
            SerializedProperty mapTargetSourceProperty,
            SerializedProperty sceneMapTargetIdProperty,
            SerializedProperty scriptMapTargetKeyProperty,
            IReadOnlyList<QuestMapTarget> availableTargets,
            int selectedIndex)
        {
            if (selectedIndex <= 0)
            {
                mapTargetSourceProperty.enumValueIndex = (int)QuestMapTargetSourceType.None;
                sceneMapTargetIdProperty.stringValue = string.Empty;
                scriptMapTargetKeyProperty.stringValue = string.Empty;
                return;
            }

            if (selectedIndex == 1)
            {
                mapTargetSourceProperty.enumValueIndex = (int)QuestMapTargetSourceType.ScriptTarget;
                sceneMapTargetIdProperty.stringValue = string.Empty;
                return;
            }

            int targetIndex = selectedIndex - 2;
            if (targetIndex < 0 || targetIndex >= availableTargets.Count || availableTargets[targetIndex] == null)
            {
                mapTargetSourceProperty.enumValueIndex = (int)QuestMapTargetSourceType.None;
                sceneMapTargetIdProperty.stringValue = string.Empty;
                scriptMapTargetKeyProperty.stringValue = string.Empty;
                return;
            }

            mapTargetSourceProperty.enumValueIndex = (int)QuestMapTargetSourceType.SceneTarget;
            sceneMapTargetIdProperty.stringValue = availableTargets[targetIndex].TargetId;
            scriptMapTargetKeyProperty.stringValue = string.Empty;
        }

        private static string BuildMapTargetLabel(QuestMapTarget mapTarget)
        {
            if (mapTarget == null)
            {
                return "<Missing>";
            }

            Transform pointerTransform = mapTarget.TargetTransform;
            string ownerPath = GetTransformPath(mapTarget.transform);
            string pointerPath = pointerTransform != null ? GetTransformPath(pointerTransform) : ownerPath;

            if (pointerPath == ownerPath)
            {
                return $"{mapTarget.gameObject.scene.name}: {ownerPath}";
            }

            return $"{mapTarget.gameObject.scene.name}: {ownerPath} -> {pointerPath}";
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
            {
                return "<Null>";
            }

            var pathParts = new List<string>();
            Transform current = target;
            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        private string GetNodeTitle(QuestNode node)
        {
            if (node.NodeData == null)
            {
                return "Quest Node";
            }

            string prefix = IsStartNode(node) ? "[Start] " : string.Empty;
            return prefix + QuestPreviewUtility.GetNodeEditorTitle(node.NodeData);
        }

        private static string GetNodeDisplayName(QuestNodeData nodeData)
        {
            if (nodeData == null)
            {
                return "Quest Node";
            }

            SerializedObject nodeDataObject = new SerializedObject(nodeData);
            SerializedProperty nameProperty = nodeDataObject.FindProperty("localizedName");
            SerializedProperty tableReferenceProperty = nameProperty?.FindPropertyRelative("m_TableReference");
            SerializedProperty tableCollectionNameProperty = tableReferenceProperty?.FindPropertyRelative("m_TableCollectionName");
            SerializedProperty entryReferenceProperty = nameProperty?.FindPropertyRelative("m_TableEntryReference");
            SerializedProperty keyProperty = entryReferenceProperty?.FindPropertyRelative("m_Key");
            SerializedProperty keyIdProperty = entryReferenceProperty?.FindPropertyRelative("m_KeyId");

            if (tableCollectionNameProperty == null || string.IsNullOrWhiteSpace(tableCollectionNameProperty.stringValue))
            {
                return "\u041d\u0435\u0442 \u0441\u0442\u0440\u043e\u043a\u0438: " + nodeData.name;
            }

            if ((keyProperty == null || string.IsNullOrWhiteSpace(keyProperty.stringValue)) &&
                (keyIdProperty == null || keyIdProperty.longValue == 0))
            {
                return "\u041d\u0435\u0442 \u0441\u0442\u0440\u043e\u043a\u0438: " + nodeData.name;
            }

            if (keyProperty != null && !string.IsNullOrWhiteSpace(keyProperty.stringValue))
            {
                return keyProperty.stringValue;
            }

            if (keyIdProperty != null && keyIdProperty.longValue != 0)
            {
                return $"Key {keyIdProperty.longValue}";
            }

            return $"Нет строки: {nodeData.name}";
        }

        private void CancelTargetSelection(bool repaint = true)
        {
            isSelectingTargetNode = false;
            pendingTransition = null;
            sourceNodeForSelection = null;

            if (repaint)
            {
                Repaint();
            }
        }

        private static void EnsureCollections(QuestNodeData nodeData)
        {
            if (nodeData == null)
            {
                return;
            }

            nodeData.Transitions ??= new List<QuestTransition>();
        }

        private void DrawTransitionDataEditor(QuestTransition transition)
        {
            SerializedObject transitionObject = new SerializedObject(transition);
            transitionObject.Update();

            SerializedProperty hasConditionsProperty = transitionObject.FindProperty("hasConditions");
            SerializedProperty conditionsProperty = transitionObject.FindProperty("conditions");
            SerializedProperty hasResultsProperty = transitionObject.FindProperty("hasResults");
            SerializedProperty resultsProperty = transitionObject.FindProperty("results");

            if (hasConditionsProperty != null && conditionsProperty != null)
            {
                EditorGUILayout.PropertyField(hasConditionsProperty, new GUIContent("Condition"));
                if (hasConditionsProperty.boolValue)
                {
                    DrawQuestResourceEntries(conditionsProperty, "Conditions");
                }
            }

            if (hasResultsProperty != null && resultsProperty != null)
            {
                EditorGUILayout.PropertyField(hasResultsProperty, new GUIContent("Rewards/Penalties"));
                if (hasResultsProperty.boolValue)
                {
                    DrawQuestResourceEntries(resultsProperty, "Rewards/Penalties");
                }
            }

            if (transitionObject.hasModifiedProperties)
            {
                transitionObject.ApplyModifiedProperties();
                MarkDirty(transition);
            }
        }

        private void DrawQuestResourceEntries(SerializedProperty entriesProperty, string label)
        {
            EditorGUILayout.LabelField(label, MiniBoldLabelStyle);

            int removeIndex = -1;
            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                SerializedProperty typeProperty = entryProperty.FindPropertyRelative("type");
                SerializedProperty moneyAmountProperty = entryProperty.FindPropertyRelative("moneyAmount");
                SerializedProperty itemConfigProperty = entryProperty.FindPropertyRelative("itemConfig");
                SerializedProperty itemCountProperty = entryProperty.FindPropertyRelative("itemCount");
                QuestResourceEntryType entryType = (QuestResourceEntryType)typeProperty.enumValueIndex;
                Color accentColor = GetQuestResourceEntryAccentColor(entryType);
                string entryTitle = GetQuestResourceEntryTitle(typeProperty);

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
                GUILayout.Label(entryTitle, CenteredMiniLabelStyle);
                GUILayout.FlexibleSpace();
                if (DrawMiniButton("X", GUILayout.Width(22f)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();
                DrawQuestEditorDivider(SectionDividerColor);
                EditorGUILayout.Space(3f);

                DrawEnumPropertyField(typeProperty, "Type");
                switch (entryType)
                {
                    case QuestResourceEntryType.Money:
                        DrawPropertyFieldWithCustomLabel(moneyAmountProperty, "Amount");
                        break;
                    case QuestResourceEntryType.Item:
                        DrawPropertyFieldWithCustomLabel(itemConfigProperty, "Item");
                        DrawPropertyFieldWithCustomLabel(itemCountProperty, "Count");
                        break;
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                if (i < entriesProperty.arraySize - 1)
                {
                    EditorGUILayout.Space(2f);
                    DrawQuestEditorDivider(SoftestDividerColor);
                    EditorGUILayout.Space(4f);
                }
                else
                {
                    EditorGUILayout.Space(2f);
                }
            }

            if (removeIndex >= 0)
            {
                entriesProperty.DeleteArrayElementAtIndex(removeIndex);
            }

            if (DrawButton($"+ Add {label} Entry"))
            {
                entriesProperty.arraySize++;
            }
        }

        private string GetTransitionFoldoutKey(QuestNodeData nodeData, int transitionIndex)
        {
            int nodeId = nodeData != null ? nodeData.GetInstanceID() : 0;
            return $"{nodeId}:{transitionIndex}";
        }

        private bool GetTransitionFoldoutState(string foldoutKey)
        {
            if (transitionFoldoutStates.TryGetValue(foldoutKey, out bool isExpanded))
            {
                return isExpanded;
            }

            transitionFoldoutStates[foldoutKey] = true;
            return true;
        }

        private void SetTransitionFoldoutState(string foldoutKey, bool isExpanded)
        {
            transitionFoldoutStates[foldoutKey] = isExpanded;
        }

        private void ClearTransitionFoldoutStates(QuestNodeData nodeData)
        {
            if (nodeData == null)
            {
                return;
            }

            string keyPrefix = $"{nodeData.GetInstanceID()}:";
            List<string> keysToRemove = transitionFoldoutStates.Keys
                .Where(key => key.StartsWith(keyPrefix))
                .ToList();

            foreach (string key in keysToRemove)
            {
                transitionFoldoutStates.Remove(key);
            }
        }

        private Color GetTransitionAccentColor(bool missingLink, bool targetOutsideGraph, bool hasConditions, bool hasResults)
        {
            if (missingLink)
            {
                return DangerAccentColor;
            }

            if (targetOutsideGraph)
            {
                return WarningAccentColor;
            }

            if (hasConditions && hasResults)
            {
                return HybridAccentColor;
            }

            if (hasConditions)
            {
                return ConditionAccentColor;
            }

            if (hasResults)
            {
                return RewardAccentColor;
            }

            return RewardAccentColor;
        }

        private static string GetTransitionStatusLabel(
            bool missingLink,
            bool targetOutsideGraph,
            bool hasConditions,
            int conditionCount,
            bool hasResults,
            int resultCount)
        {
            if (missingLink)
            {
                return "Missing Target";
            }

            if (targetOutsideGraph)
            {
                return "Outside Graph";
            }

            if (hasConditions && hasResults)
            {
                return $"C:{conditionCount} / R:{resultCount}";
            }

            if (hasConditions)
            {
                return conditionCount > 0
                    ? $"Conditions: {conditionCount}"
                    : "Has Conditions";
            }

            if (hasResults)
            {
                return resultCount > 0
                    ? $"Rewards: {resultCount}"
                    : "Has Rewards";
            }

            return "Linked";
        }

        private Color GetQuestResourceEntryAccentColor(QuestResourceEntryType entryType)
        {
            return entryType switch
            {
                QuestResourceEntryType.Money => MoneyAccentColor,
                QuestResourceEntryType.Item => ItemAccentColor,
                _ => NeutralAccentColor
            };
        }

        private static string GetQuestResourceEntryTitle(SerializedProperty typeProperty)
        {
            if (typeProperty == null || typeProperty.propertyType != SerializedPropertyType.Enum)
            {
                return "Entry";
            }

            string[] displayNames = typeProperty.enumDisplayNames;
            int index = typeProperty.enumValueIndex;
            if (displayNames == null || index < 0 || index >= displayNames.Length)
            {
                return "Entry";
            }

            return displayNames[index];
        }

        private void DrawQuestEditorDivider(Color color)
        {
            Rect dividerRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(dividerRect, color);
        }

        private bool CanAssignNodeToCurrentGraph(QuestNodeData newNodeData, QuestNodeData currentNodeData)
        {
            return newNodeData == null ||
                   newNodeData == currentNodeData ||
                   newNodeData.OwnerGraph == null ||
                   newNodeData.OwnerGraph == currentGraph;
        }

        private void AssignNodeToCurrentGraph(QuestNodeData nodeData)
        {
            if (nodeData == null)
            {
                return;
            }

            nodeData.SetOwnerGraph(currentGraph);
            MarkDirty(nodeData);
        }

        private void ReleaseNodeOwnershipIfUnused(QuestNodeData nodeData)
        {
            if (nodeData == null || currentGraph == null || currentGraph.Nodes.Any(node => node.NodeData == nodeData))
            {
                return;
            }

            nodeData.ClearOwnerGraph(currentGraph);
            MarkDirty(nodeData);
        }

        private void ClaimUnownedNodes()
        {
            if (currentGraph == null)
            {
                return;
            }

            foreach (QuestNode node in currentGraph.Nodes)
            {
                if (node?.NodeData == null || node.NodeData.OwnerGraph != null)
                {
                    continue;
                }

                node.NodeData.SetOwnerGraph(currentGraph);
                MarkDirty(node.NodeData);
            }
        }

        private static void MarkDirty(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
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

        private static Rect ExpandRect(Rect rect, float margin)
        {
            return Rect.MinMaxRect(rect.xMin - margin, rect.yMin - margin, rect.xMax + margin, rect.yMax + margin);
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

        private static bool ApproximatelyEqual(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }

        private float DrawLocalizedFieldArea(float x, float y, float width, SerializedProperty property, string label)
        {
            float areaHeight = GetLocalizedFieldAreaHeight(property);
            GUILayout.BeginArea(new Rect(x, y, width, areaHeight));
            DrawLocalizedStringSelector(property, label);
            GUILayout.EndArea();
            return y + areaHeight + 6f;
        }

        private static float GetLocalizedFieldAreaHeight(SerializedProperty localizedStringProperty)
        {
            const float baseHeight = 78f;

            string previewText = GetLocalizedStringPreview(localizedStringProperty, PreferredPreviewLocale);
            if (string.IsNullOrWhiteSpace(previewText))
            {
                return baseHeight;
            }

            GUIStyle previewStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };

            float previewHeight = Mathf.Max(
                LocalizedPreviewMinHeight,
                previewStyle.CalcHeight(new GUIContent(previewText), LocalizedPreviewWidth));

            return baseHeight + 24f + previewHeight;
        }

        private static void InvalidateStaticEditorCaches()
        {
            cachedStringTableCollections = null;
            cachedStringTableOptions = null;
            localizedEntryOptionsCache.Clear();
        }

        private void HandleProjectChanged()
        {
            InvalidateStaticEditorCaches();
            Repaint();
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

            int selectedEntryIndex = GetSelectedEntryIndex(entryOptions.Entries, keyIdProperty.longValue, keyProperty.stringValue);
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
                if (string.Equals(serializedTableReference, guidReference, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(serializedTableReference, collection.TableCollectionName, System.StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static int GetSelectedEntryIndex(IReadOnlyList<SharedTableData.SharedTableEntry> entries, long keyId, string keyName)
        {
            if (keyId != 0)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Id == keyId)
                    {
                        return i + 1;
                    }
                }
            }

            if (!string.IsNullOrEmpty(keyName))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (string.Equals(entries[i].Key, keyName, System.StringComparison.Ordinal))
                    {
                        return i + 1;
                    }
                }
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
                    if (string.Equals(serializedTableReference, guidReference, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(serializedTableReference, collection.TableCollectionName, System.StringComparison.Ordinal))
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

            object picker = System.Activator.CreateInstance(pickerType, context, "string table entry", tableProperty, entryProperty);
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

            var provider = System.Activator.CreateInstance(providerType) as UnityEditor.Search.SearchProvider;
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

        private void DrawLocalizedStringPreview(SerializedProperty localizedStringProperty)
        {
            string previewText = GetLocalizedStringPreview(localizedStringProperty, PreferredPreviewLocale);
            if (string.IsNullOrWhiteSpace(previewText))
            {
                return;
            }

            EditorGUILayout.LabelField("RU Preview", MiniBoldLabelStyle);

            GUIStyle previewStyle = new GUIStyle(PreviewLabelStyle)
            {
                wordWrap = true
            };

            float width = LocalizedPreviewWidth;
            float height = Mathf.Max(LocalizedPreviewMinHeight, previewStyle.CalcHeight(new GUIContent(previewText), width));

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
            if (string.IsNullOrWhiteSpace(serializedTableReference))
            {
                return null;
            }

            foreach (StringTableCollection collection in GetCachedStringTableCollections())
            {
                string guidReference = $"GUID:{collection.SharedData.TableCollectionNameGuid:N}";
                if (string.Equals(serializedTableReference, guidReference, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(serializedTableReference, collection.TableCollectionName, System.StringComparison.Ordinal))
                {
                    return collection;
                }
            }

            return null;
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
            if (collection == null || entryId == 0 || string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            foreach (StringTable table in collection.StringTables)
            {
                if (table == null || table.LocaleIdentifier.Code != localeCode)
                {
                    continue;
                }

                StringTableEntry entry = table.GetEntry(entryId);
                if (entry != null && !string.IsNullOrWhiteSpace(entry.LocalizedValue))
                {
                    return entry.LocalizedValue;
                }
            }

            return string.Empty;
        }

        private static System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> GetCachedStringTableCollections()
        {
            cachedStringTableCollections ??= LocalizationEditorSettings.GetStringTableCollections();
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
                .OrderBy(entry => entry.Key, System.StringComparer.Ordinal)
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

        private readonly struct CachedLocalizedEntryOptions
        {
            public static CachedLocalizedEntryOptions Empty { get; } =
                new(System.Array.Empty<SharedTableData.SharedTableEntry>(), new[] { "<None>" });

            public CachedLocalizedEntryOptions(IReadOnlyList<SharedTableData.SharedTableEntry> entries, string[] options)
            {
                Entries = entries;
                Options = options;
            }

            public IReadOnlyList<SharedTableData.SharedTableEntry> Entries { get; }
            public string[] Options { get; }
        }

        private sealed class LocalizedEntrySelectorWindow : EditorWindow
        {
            [System.Serializable]
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
            [System.NonSerialized] private SearchField searchField;

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
            }

            private void OnGUI()
            {
                EnsureSearchField();

                if (focusSearchField)
                {
                    searchField.SetFocus();
                    focusSearchField = false;
                }

                EditorGUILayout.LabelField("Select Entry", EditorStyles.boldLabel);
                searchText = searchField.OnGUI(EditorGUILayout.GetControlRect(), searchText);
                EditorGUILayout.Space(4f);

                if (GUILayout.Button("<None>", selectedIndex < 0 ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                {
                    ApplyEntrySelectionToObject(targetObject, keyIdPropertyPath, keyPropertyPath, entries, 0);
                    Close();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.Space(4f);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                bool hasVisibleEntries = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    EntryOption entry = entries[i];
                    if (!MatchesSearch(entry, searchText))
                    {
                        continue;
                    }

                    hasVisibleEntries = true;
                    GUIStyle style = i == selectedIndex ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                    if (GUILayout.Button(entry.Key, style))
                    {
                        ApplyEntrySelectionToObject(targetObject, keyIdPropertyPath, keyPropertyPath, entries, i + 1);
                        Close();
                        GUIUtility.ExitGUI();
                    }
                }

                if (!hasVisibleEntries)
                {
                    EditorGUILayout.HelpBox("No entries found.", MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
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
                if (activeWindow == this)
                {
                    activeWindow = null;
                }
            }

            private void EnsureSearchField()
            {
                searchField ??= new SearchField();
            }

            private static bool MatchesSearch(EntryOption entry, string searchText)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    return true;
                }

                return entry.Key?.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
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

        private void DrawBackgroundGrid(Rect rect)
        {
            Handles.BeginGUI();

            EditorGUI.DrawRect(rect, CanvasBackgroundColor);

            DrawGridLines(rect, 20f, MinorGridColor);
            DrawGridLines(rect, 100f, MajorGridColor);

            Handles.EndGUI();
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
            EditorGUI.PropertyField(fieldRect, property, GUIContent.none, includeChildren);
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

        private Color HybridAccentColor => useLightTheme
            ? new Color(0.20f, 0.58f, 0.64f, 1f)
            : new Color(0.24f, 0.72f, 0.78f, 1f);

        private Color ConditionAccentColor => useLightTheme
            ? new Color(0.21f, 0.52f, 0.72f, 1f)
            : new Color(0.26f, 0.63f, 0.86f, 1f);

        private Color RewardAccentColor => useLightTheme
            ? new Color(0.31f, 0.65f, 0.35f, 1f)
            : new Color(0.38f, 0.78f, 0.42f, 1f);

        private Color MoneyAccentColor => useLightTheme
            ? new Color(0.74f, 0.58f, 0.18f, 1f)
            : new Color(0.85f, 0.68f, 0.22f, 1f);

        private Color ItemAccentColor => useLightTheme
            ? new Color(0.20f, 0.66f, 0.64f, 1f)
            : new Color(0.24f, 0.78f, 0.76f, 1f);

        private Color NeutralAccentColor => useLightTheme
            ? new Color(0.48f, 0.48f, 0.48f, 1f)
            : new Color(0.60f, 0.60f, 0.60f, 1f);

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

        private void DrawGridLines(Rect rect, float spacing, Color color)
        {
            Handles.color = color;

            for (float x = rect.xMin; x <= rect.xMax; x += spacing)
            {
                Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
            }

            for (float y = rect.yMin; y <= rect.yMax; y += spacing)
            {
                Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
            }
        }
    }
}
