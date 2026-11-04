using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EditorTools;
using Quests.Editor;
using Quests.MapTargets;
using Quests.Graph.Model;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Quests.Graph.Editor
{
    public partial class QuestEditorWindow : GraphEditorWindowBase, IQuestNodeInspectorHost
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
        private string nodesFolderPath;
        private string transitionsFolderPath;
        private readonly Dictionary<QuestNode, Rect> nodeRects = new();
        private readonly Dictionary<QuestNodeData, QuestNode> nodeDataToNodeLookup = new();
        private readonly QuestGraphConnectionPresenter connectionPresenter = new();
        private readonly GUIContent localizedPreviewContent = new();
        private readonly GraphEditorNodeLayoutSynchronizer<QuestNode> nodeLayoutSynchronizer = new();
        private readonly Dictionary<string, bool> transitionFoldoutStates = new();
        private static System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> cachedStringTableCollections;
        private static string[] cachedStringTableOptions;
        private static readonly Dictionary<string, CachedLocalizedEntryOptions> localizedEntryOptionsCache = new();

        private bool isSelectingTargetNode;
        private bool graphStructureDirty = true;
        private QuestTransition pendingTransition;
        private QuestNode sourceNodeForSelection;
        private QuestNode activeConnectionNode;
        private GraphEditorTheme theme;
        private readonly GraphEditorOverlayPanelState controlsPanel = new();

        private RetainedGraphCanvas<QuestNode, QuestTransition> toolkitCanvas;
        private DelegatingRetainedGraphCanvasAdapter<QuestNode, QuestTransition> canvasAdapter;
        private IMGUIContainer toolkitControls;

        private GraphEditorTheme Theme => theme ??= new GraphEditorTheme(() => useLightTheme);

        [MenuItem("Tools/Quest Editor")]
        public static void Open()
        {
            GetWindow<QuestEditorWindow>("Quest Editor");
        }

        private void CreateGUI()
        {
            canvasAdapter = CreateCanvasAdapter();
            toolkitCanvas = new RetainedGraphCanvas<QuestNode, QuestTransition>(
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter);
            toolkitControls = GraphEditorWindowUi.BuildRoot(
                rootVisualElement,
                toolkitCanvas,
                "quest-editor-controls",
                OverlayPanelWidth,
                WindowBackgroundColor,
                DrawToolkitControls);

            UpdateControlsInputWidth();
            toolkitCanvas.RebuildNow();
        }

        private void OnEnable()
        {
            nodesFolderPath = GraphEditorPreferences.LoadFolder(NodesPathKey, "Assets/QuestNodes");
            transitionsFolderPath = GraphEditorPreferences.LoadFolder(TransitionsPathKey, "Assets/QuestTransitions");
            useLightTheme = GraphEditorPreferences.LoadTheme(ThemeKey);
            GraphEditorAssetChangeTracker.AssetsChanged += HandleTrackedAssetsChanged;
        }

        private void OnDisable()
        {
            GraphEditorAssetChangeTracker.AssetsChanged -= HandleTrackedAssetsChanged;
            theme?.Dispose();
            theme = null;
            toolkitCanvas = null;
            canvasAdapter = null;
            toolkitControls = null;
        }

        private void DrawToolkitControls()
        {
            using (BeginThemedGuiScope())
            {
                DrawControlsOverlay();
            }
        }

        private void DrawToolkitNode(QuestNode node)
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

        private DelegatingRetainedGraphCanvasAdapter<QuestNode, QuestTransition> CreateCanvasAdapter()
        {
            return new DelegatingRetainedGraphCanvasAdapter<QuestNode, QuestTransition>(
                "Create or load a quest graph.",
                NodeSize,
                () => currentGraph != null,
                PrepareRetainedGraph,
                ClearRetainedNodeRects,
                GetRetainedGraphNodes,
                GetRetainedGraphConnections,
                GetRetainedNodePosition,
                SetRetainedNodePosition,
                SetRetainedNodeRect,
                GetRetainedNodeTitle,
                GetRetainedNodeTint,
                IsRetainedNodeTargetable,
                DrawRetainedNode,
                DeleteRetainedNode,
                SelectRetainedNode,
                ClearRetainedNodeSelection,
                TrySelectRetainedTarget,
                MarkRetainedNodePositionDirty,
                ClampRetainedGraphPan,
                DrawRetainedConnection,
                () => zoom,
                value => zoom = value,
                () => panOffset,
                value => panOffset = value,
                () => isSelectingTargetNode,
                () => CanvasBackgroundColor,
                () => PanelBackgroundColor,
                () => MinorGridColor,
                () => MajorGridColor,
                () => GetSelectionOverlayColor(false));
        }

        void PrepareRetainedGraph()
        {
            if (currentGraph == null)
            {
                return;
            }

            if (graphStructureDirty)
            {
                CleanupGraph();
                graphStructureDirty = false;
            }

            EnsureGraphNodes();
            SynchronizeNodeRects();
        }

        void ClearRetainedNodeRects()
        {
            nodeRects.Clear();
            nodeDataToNodeLookup.Clear();
            connectionPresenter.ClearNodePresentationState();
        }

        IEnumerable<QuestNode> GetRetainedGraphNodes()
        {
            return currentGraph?.Nodes ?? Enumerable.Empty<QuestNode>();
        }

        IEnumerable<RetainedGraphConnection<QuestNode, QuestTransition>>
            GetRetainedGraphConnections()
        {
            if (currentGraph == null)
            {
                yield break;
            }

            foreach (QuestNode sourceNode in currentGraph.Nodes)
            {
                foreach (RetainedGraphConnection<QuestNode, QuestTransition> connection in GetRetainedGraphConnectionsFrom(sourceNode))
                {
                    yield return connection;
                }
            }
        }

        IEnumerable<RetainedGraphConnection<QuestNode, QuestTransition>> GetRetainedGraphConnectionsFrom(QuestNode sourceNode)
        {
            if (sourceNode?.NodeData == null)
            {
                yield break;
            }

            EnsureCollections(sourceNode.NodeData);
            foreach (QuestTransition transition in sourceNode.NodeData.Transitions)
            {
                if (transition?.TargetNode != null &&
                    nodeDataToNodeLookup.TryGetValue(transition.TargetNode, out QuestNode targetNode))
                {
                    yield return new RetainedGraphConnection<QuestNode, QuestTransition>(
                        sourceNode,
                        targetNode,
                        transition);
                }
            }
        }

        Vector2 GetRetainedNodePosition(QuestNode node)
        {
            return node.Position;
        }

        void SetRetainedNodePosition(QuestNode node, Vector2 position)
        {
            node.Position = position;
        }

        void SetRetainedNodeRect(QuestNode node, Rect rect)
        {
            nodeRects[node] = rect;
        }

        string GetRetainedNodeTitle(QuestNode node)
        {
            return GetNodeTitle(node);
        }

        Color GetRetainedNodeTint(QuestNode node)
        {
            return GetNodeTint(node);
        }

        bool IsRetainedNodeTargetable(QuestNode node)
        {
            return node != null && node != sourceNodeForSelection && node.NodeData != null;
        }

        void DrawRetainedNode(QuestNode node)
        {
            DrawToolkitNode(node);
        }

        void DeleteRetainedNode(QuestNode node)
        {
            DeleteNode(node, false);
        }

        void SelectRetainedNode(QuestNode node)
        {
            activeConnectionNode = node;
        }

        void ClearRetainedNodeSelection()
        {
            activeConnectionNode = null;
        }

        bool TrySelectRetainedTarget(QuestNode node)
        {
            if (!isSelectingTargetNode || pendingTransition == null ||
                node == null || node == sourceNodeForSelection || node.NodeData == null)
            {
                return false;
            }

            pendingTransition.SetTargetNode(node.NodeData);
            MarkDirty(pendingTransition);
            toolkitCanvas?.RebuildConnectionsGradually();
            CancelTargetSelection(false);
            return true;
        }

        void MarkRetainedNodePositionDirty()
        {
            MarkNodePositionDirty();
        }

        void ClampRetainedGraphPan(float workspaceWidth, float workspaceHeight)
        {
            ClampPanToWorkspace(workspaceWidth, workspaceHeight);
        }

        void DrawRetainedConnection(
            Painter2D painter,
            QuestTransition transition,
            QuestNode sourceNode,
            QuestNode targetNode,
            Rect sourceRect,
            Rect targetRect,
            bool isDragging)
        {
            connectionPresenter.DrawRetainedConnection(
                painter,
                transition,
                sourceNode,
                targetNode,
                sourceRect,
                targetRect,
                isDragging,
                nodeRects,
                GetConnectionColor(sourceNode, targetNode));
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

            Rect toggleRect = controlsPanel.GetToggleRect(
                panelRect,
                toggleButtonWidth,
                toggleButtonHeight,
                collapsedToggleLeftOffset);

            if (!controlsPanel.IsExpanded)
            {
                if (DrawButton(toggleRect, ">"))
                {
                    SetControlsPanelExpanded(true);
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
                GraphEditorPreferences.SaveFolder(NodesPathKey, nodesFolderPath);
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
                GraphEditorPreferences.SaveFolder(TransitionsPathKey, transitionsFolderPath);
            }

            y += 36f;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), GetThemeToggleLabel()))
            {
                useLightTheme = !useLightTheme;
                GraphEditorPreferences.SaveTheme(ThemeKey, useLightTheme);
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
                y += GraphEditorValidationGui.DrawSummary(
                    padding, y, contentWidth, QuestGraphValidator.Validate(currentGraph));

                GUILayout.BeginArea(new Rect(padding, y, contentWidth, 112f));
                QuestPreviewUtility.DrawQuestGraphPreview(currentGraph, "Quest");
                GUILayout.EndArea();
                y += 118f;

                SerializedObject graphObject = new SerializedObject(currentGraph);
                graphObject.Update();
                SerializedProperty titleProperty = graphObject.FindProperty("title");
                SerializedProperty descriptionProperty = graphObject.FindProperty("description");
                SerializedProperty iconProperty = graphObject.FindProperty("icon");
                if (titleProperty != null)
                {
                    y = DrawLocalizedFieldArea(padding, y, contentWidth, titleProperty, "Name");
                }

                if (descriptionProperty != null)
                {
                    y = DrawLocalizedFieldArea(padding, y, contentWidth, descriptionProperty, "Description");
                }

                if (iconProperty != null)
                {
                    GUILayout.BeginArea(new Rect(padding, y, contentWidth, EditorGUIUtility.singleLineHeight));
                    DrawPropertyFieldWithCustomLabel(iconProperty, "Map Popup Sprite");
                    GUILayout.EndArea();
                    y += EditorGUIUtility.singleLineHeight + spacing;
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
                SetControlsPanelExpanded(false);
            }
        }

        private void SetControlsPanelExpanded(bool isExpanded)
        {
            controlsPanel.SetExpanded(isExpanded, toolkitControls, OverlayPanelWidth);
        }

        private void UpdateControlsInputWidth()
        {
            controlsPanel.ApplyInputWidth(toolkitControls, OverlayPanelWidth);
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
            currentGraph = QuestGraphEditorCommands.CreateGraph();
            RefreshToolkitCanvas(true);
        }

        private void LoadGraph()
        {
            if (!GraphEditorAssetService.TryLoadAsset("Load Quest Graph", out currentGraph))
            {
                return;
            }

            EnsureGraphNodes();
            ClaimUnownedNodes();
            graphStructureDirty = true;
            RefreshToolkitCanvas(true);
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

            if (QuestGraphEditorCommands.TryCreateNode(
                    currentGraph, nodesFolderPath, GetCenteredNodePosition(NodeSize), out QuestNode node))
            {
                MarkDirty(currentGraph);
                MarkDirty(node.NodeData);
                RefreshToolkitCanvas(true);
            }
        }

        private void SynchronizeNodeRects()
        {
            GraphEditorNodeLookup.Rebuild(currentGraph.Nodes, nodeDataToNodeLookup, node => node.NodeData);

            if (nodeLayoutSynchronizer.Synchronize(currentGraph.Nodes, nodeRects, node => node.Position, NodeSize))
            {
                InvalidateConnectionRouteCache();
            }
        }

        private void InvalidateConnectionRouteCache()
        {
            connectionPresenter.InvalidateRoutes();
        }

        private void CleanupGraph()
        {
            if (currentGraph == null)
            {
                return;
            }

            bool graphChanged = QuestGraphStructureOperations.EnsureNodes(currentGraph);
            graphChanged |= QuestGraphStructureOperations.RemoveMissingNodes(currentGraph);

            if (graphChanged)
            {
                MarkDirty(currentGraph);
            }
        }

        private Color GetConnectionColor(QuestNode sourceNode, QuestNode targetNode)
        {
            return GraphEditorConnectionPresentation.GetColor(
                sourceNode,
                targetNode,
                activeConnectionNode,
                PrimaryConnectionColor,
                SourceHighlightConnectionColor,
                TargetHighlightConnectionColor);
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
                connectionPresenter.RecordPicker(transition, GUILayoutUtility.GetLastRect());

                if (pickPressed)
                {
                    isSelectingTargetNode = true;
                    pendingTransition = transition;
                    sourceNodeForSelection = ownerNode;
                    RefreshToolkitCanvas();
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
            if (QuestGraphEditorCommands.TryCreateTransition(nodeData, transitionsFolderPath))
            {
                MarkDirty(nodeData);
            }
        }

        private void RemoveTransition(QuestNodeData nodeData, int removeTransitionIndex)
        {
            QuestTransition removedTransition = nodeData.Transitions[removeTransitionIndex];
            GraphEditorAssetService.MarkDirty(nodeData, "Remove quest transition");
            nodeData.Transitions.RemoveAt(removeTransitionIndex);
            MarkDirty(nodeData);

            if (removedTransition != null && QuestGraphReferenceOperations.CanDeleteTransitionAsset(removedTransition))
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
                        GraphEditorAssetService.DeleteAsset(removedTransition, "Delete quest transition");
                    }
                }
            }

            GraphEditorAssetService.FlushChanges();
            Selection.activeObject = null;
            GUIUtility.ExitGUI();
        }

        private void DeleteNode(QuestNode node, bool exitGui = true)
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
                    QuestGraphReferenceOperations.DeleteUnsharedTransitions(node.NodeData);

                    GraphEditorAssetService.DeleteAsset(node.NodeData, "Delete quest node");
                }
            }

            GraphEditorAssetService.MarkDirty(currentGraph, "Delete quest node");
            currentGraph.Nodes.Remove(node);
            if (!shouldDeleteNodeAsset)
            {
                ReleaseNodeOwnershipIfUnused(node.NodeData);
            }

            MarkDirty(currentGraph);
            GraphEditorAssetService.FlushChanges();
            RefreshToolkitCanvas(true);
            if (exitGui)
            {
                GUIUtility.ExitGUI();
            }
        }

        private void RemoveNodeReferences(QuestNodeData nodeData)
        {
            QuestGraphReferenceOperations.RemoveIncomingReferences(currentGraph, nodeData);

            if ((sourceNodeForSelection != null && sourceNodeForSelection.NodeData == nodeData) ||
                (pendingTransition != null && pendingTransition.TargetNode == nodeData))
            {
                CancelTargetSelection();
            }
        }

        private void ReplaceNodeReferences(QuestNodeData oldNode, QuestNodeData newNode)
        {
            QuestGraphReferenceOperations.ReplaceIncomingReferences(currentGraph, oldNode, newNode);
        }

        private void EnsureGraphNodes()
        {
            if (QuestGraphStructureOperations.EnsureNodes(currentGraph)) MarkDirty(currentGraph);
        }

        private QuestNodeData GetStartNode()
        {
            return QuestGraphStructureOperations.GetStartNode(currentGraph);
        }

        private bool ContainsNode(QuestNodeData nodeData)
        {
            return QuestGraphStructureOperations.ContainsNode(currentGraph, nodeData);
        }

        private bool IsStartNode(QuestNode node)
        {
            return QuestGraphStructureOperations.IsStartNode(currentGraph, node);
        }

        private bool IsOrphanNode(QuestNodeData nodeData)
        {
            return QuestGraphStructureOperations.IsOrphanNode(currentGraph, nodeData);
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
            RefreshToolkitCanvas();
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
            SerializedProperty descriptionProperty = nodeDataObject.FindProperty("localizedDescription");
            SerializedProperty iconProperty = nodeDataObject.FindProperty("icon");
            SerializedProperty mapTargetSourceProperty = nodeDataObject.FindProperty("mapTargetSource");
            SerializedProperty sceneMapTargetIdProperty = nodeDataObject.FindProperty("sceneMapTargetId");
            SerializedProperty authoredMapPositionProperty = nodeDataObject.FindProperty("authoredMapPosition");
            SerializedProperty hasAuthoredMapPositionProperty = nodeDataObject.FindProperty("hasAuthoredMapPosition");
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

            if (descriptionProperty != null)
            {
                DrawLocalizedStringSelector(descriptionProperty, "Description");
            }

            if (iconProperty != null)
            {
                DrawPropertyFieldWithCustomLabel(iconProperty, "Sprite");
            }

            DrawMapTargetSelector(
                nodeData,
                mapTargetSourceProperty,
                sceneMapTargetIdProperty,
                scriptMapTargetKeyProperty,
                authoredMapPositionProperty,
                hasAuthoredMapPositionProperty);

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
            SerializedProperty scriptMapTargetKeyProperty,
            SerializedProperty authoredMapPositionProperty,
            SerializedProperty hasAuthoredMapPositionProperty)
        {
            if (nodeData == null ||
                mapTargetSourceProperty == null ||
                sceneMapTargetIdProperty == null ||
                scriptMapTargetKeyProperty == null ||
                authoredMapPositionProperty == null ||
                hasAuthoredMapPositionProperty == null)
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
                    authoredMapPositionProperty,
                    hasAuthoredMapPositionProperty,
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

            return UnityEngine.Object.FindObjectsByType<QuestMapTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
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
            SerializedProperty authoredMapPositionProperty,
            SerializedProperty hasAuthoredMapPositionProperty,
            IReadOnlyList<QuestMapTarget> availableTargets,
            int selectedIndex)
        {
            if (selectedIndex <= 0)
            {
                mapTargetSourceProperty.enumValueIndex = (int)QuestMapTargetSourceType.None;
                sceneMapTargetIdProperty.stringValue = string.Empty;
                scriptMapTargetKeyProperty.stringValue = string.Empty;
                hasAuthoredMapPositionProperty.boolValue = false;
                return;
            }

            if (selectedIndex == 1)
            {
                mapTargetSourceProperty.enumValueIndex = (int)QuestMapTargetSourceType.ScriptTarget;
                sceneMapTargetIdProperty.stringValue = string.Empty;
                hasAuthoredMapPositionProperty.boolValue = false;
                return;
            }

            int targetIndex = selectedIndex - 2;
            if (targetIndex < 0 || targetIndex >= availableTargets.Count || availableTargets[targetIndex] == null)
            {
                mapTargetSourceProperty.enumValueIndex = (int)QuestMapTargetSourceType.None;
                sceneMapTargetIdProperty.stringValue = string.Empty;
                scriptMapTargetKeyProperty.stringValue = string.Empty;
                hasAuthoredMapPositionProperty.boolValue = false;
                return;
            }

            mapTargetSourceProperty.enumValueIndex = (int)QuestMapTargetSourceType.SceneTarget;
            sceneMapTargetIdProperty.stringValue = availableTargets[targetIndex].TargetId;
            scriptMapTargetKeyProperty.stringValue = string.Empty;
            authoredMapPositionProperty.vector3Value = availableTargets[targetIndex].TargetTransform.position;
            hasAuthoredMapPositionProperty.boolValue = true;
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
                RefreshToolkitCanvas();
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
            SerializedProperty executeAutomaticallyWhenAvailableProperty = transitionObject.FindProperty("executeAutomaticallyWhenAvailable");

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

            if (executeAutomaticallyWhenAvailableProperty != null)
            {
                EditorGUILayout.PropertyField(
                    executeAutomaticallyWhenAvailableProperty,
                    new GUIContent(
                        "Advance Automatically When Available",
                        "Executes this transition when its resource conditions become satisfied, without a dialogue selection."));
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
            QuestGraphReferenceOperations.ReleaseOwnershipIfUnused(currentGraph, nodeData);
        }

        private void ClaimUnownedNodes()
        {
            QuestGraphReferenceOperations.ClaimUnownedNodes(currentGraph);
        }

        private void MarkDirty(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            InvalidateConnectionRouteCache();
        }

        private void MarkNodePositionDirty()
        {
            if (currentGraph != null)
            {
                EditorUtility.SetDirty(currentGraph);
            }
        }

        private float DrawLocalizedFieldArea(float x, float y, float width, SerializedProperty property, string label)
        {
            float areaHeight = GetLocalizedFieldAreaHeight(property);
            GUILayout.BeginArea(new Rect(x, y, width, areaHeight));
            DrawLocalizedStringSelector(property, label);
            GUILayout.EndArea();
            return y + areaHeight + 6f;
        }

        private float GetLocalizedFieldAreaHeight(SerializedProperty localizedStringProperty)
        {
            const float baseHeight = 78f;

            string previewText = GetLocalizedStringPreview(localizedStringProperty, PreferredPreviewLocale);
            if (string.IsNullOrWhiteSpace(previewText))
            {
                return baseHeight;
            }

            GUIStyle previewStyle = PreviewLabelStyle;
            localizedPreviewContent.text = previewText;

            float previewHeight = Mathf.Max(
                LocalizedPreviewMinHeight,
                previewStyle.CalcHeight(localizedPreviewContent, LocalizedPreviewWidth));

            return baseHeight + 24f + previewHeight;
        }

        private static void InvalidateStaticEditorCaches()
        {
            cachedStringTableCollections = null;
            cachedStringTableOptions = null;
            localizedEntryOptionsCache.Clear();
        }

        private void HandleTrackedAssetsChanged(IReadOnlyCollection<string> changedAssetPaths)
        {
            if (!HasRelevantAssetChange(changedAssetPaths))
            {
                return;
            }

            InvalidateStaticEditorCaches();
            InvalidateConnectionRouteCache();
            bool graphAssetChanged = currentGraph != null &&
                changedAssetPaths.Contains(AssetDatabase.GetAssetPath(currentGraph));
            RefreshToolkitCanvas(graphAssetChanged || HasMissingNodeReference());
        }

        private bool HasRelevantAssetChange(IReadOnlyCollection<string> changedAssetPaths)
        {
            return GraphEditorAssetChangeFilter.HasRelevantChange(
                changedAssetPaths,
                currentGraph != null ? AssetDatabase.GetAssetPath(currentGraph) : null,
                nodesFolderPath,
                transitionsFolderPath);
        }

        private bool HasMissingNodeReference()
        {
            return currentGraph != null && currentGraph.Nodes.Any(node =>
                node == null ||
                node.NodeData == null ||
                !AssetDatabase.Contains(node.NodeData) ||
                (node.NodeData.Transitions != null && node.NodeData.Transitions.Any(transition =>
                    transition == null || !AssetDatabase.Contains(transition))));
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
                DrawLocalizedStringValidationAndPreview(localizedStringProperty);
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

            DrawLocalizedStringValidationAndPreview(localizedStringProperty);
        }

        private void DrawLocalizedStringValidationAndPreview(SerializedProperty localizedStringProperty)
        {
            SerializedProperty tableReferenceProperty = localizedStringProperty?.FindPropertyRelative("m_TableReference");
            SerializedProperty entryReferenceProperty = localizedStringProperty?.FindPropertyRelative("m_TableEntryReference");
            SerializedProperty tableCollectionNameProperty = tableReferenceProperty?.FindPropertyRelative("m_TableCollectionName");
            SerializedProperty keyIdProperty = entryReferenceProperty?.FindPropertyRelative("m_KeyId");
            SerializedProperty keyProperty = entryReferenceProperty?.FindPropertyRelative("m_Key");

            StringTableCollection collection = ResolveStringTableCollection(tableCollectionNameProperty?.stringValue);
            if (collection == null)
            {
                EditorGUILayout.HelpBox("Select a valid localization table.", MessageType.None);
                return;
            }

            if (ResolveSharedTableEntry(collection, keyIdProperty, keyProperty) == null)
            {
                EditorGUILayout.HelpBox("Select a localization entry.", MessageType.None);
                return;
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
                IndicesById = new Dictionary<long, int>(entries.Count);
                IndicesByKey = new Dictionary<string, int>(entries.Count, System.StringComparer.Ordinal);
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

        private string GetThemeToggleLabel() => Theme.GetThemeToggleLabel();

        protected override void ApplyThemedGuiState() => Theme.ApplyGuiState();

        protected override void RestoreThemedGuiState() => Theme.RestoreGuiState();

        private void DrawWindowBackground() =>
            Theme.DrawWindowBackground(new Rect(0f, 0f, position.width, position.height));

        private bool DrawButton(Rect rect, string label) => GUI.Button(rect, label, ButtonStyle);

        private bool DrawMiniButton(Rect rect, string label) => GUI.Button(rect, label, MiniButtonStyle);

        private bool DrawButton(string label, params GUILayoutOption[] options) =>
            GUILayout.Button(label, ButtonStyle, options);

        private bool DrawMiniButton(string label, params GUILayoutOption[] options) =>
            GUILayout.Button(label, MiniButtonStyle, options);

        private GUIStyle NodeWindowStyle => Theme.NodeWindowStyle;
        private GUIStyle HelpBoxStyle => Theme.HelpBoxStyle;
        private GUIStyle ButtonStyle => Theme.ButtonStyle;
        private GUIStyle MiniButtonStyle => Theme.MiniButtonStyle;
        private GUIStyle PopupStyle => Theme.PopupStyle;
        private GUIStyle TextFieldStyle => Theme.TextFieldStyle;
        private GUIStyle LabelStyle => Theme.LabelStyle;
        private GUIStyle FoldoutStyle => Theme.FoldoutStyle;
        private GUIStyle BoldLabelStyle => Theme.BoldLabelStyle;
        private GUIStyle MiniBoldLabelStyle => Theme.MiniBoldLabelStyle;
        private GUIStyle MiniLabelStyle => Theme.MiniLabelStyle;
        private GUIStyle WordWrappedMiniLabelStyle => Theme.WordWrappedMiniLabelStyle;
        private GUIStyle CenteredMiniLabelStyle => Theme.CenteredMiniLabelStyle;
        private GUIStyle PreviewLabelStyle => Theme.PreviewLabelStyle;

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

        private Color PanelBackgroundColor => Theme.PanelBackgroundColor;
        private Color CanvasBackgroundColor => Theme.CanvasBackgroundColor;
        private Color MinorGridColor => Theme.MinorGridColor;
        private Color MajorGridColor => Theme.MajorGridColor;
        private Color PrimaryConnectionColor => Theme.PrimaryConnectionColor;
        private Color SourceHighlightConnectionColor => Theme.SourceHighlightConnectionColor;
        private Color TargetHighlightConnectionColor => Theme.TargetHighlightConnectionColor;
        private Color WindowBackgroundColor => Theme.WindowBackgroundColor;
        private Color DangerButtonColor => Theme.DangerButtonColor;
        private Color LinkButtonColor => Theme.LinkButtonColor;
        private Color StartBadgeColor => Theme.StartBadgeColor;
        private Color WarningBadgeColor => Theme.WarningBadgeColor;
        private Color StartNodeTint => Theme.StartNodeTint;
        private Color OrphanNodeTint => Theme.OrphanNodeTint;
        private Color SelectionOverlayTextColor => Theme.SelectionOverlayTextColor;
        private Color DangerAccentColor => Theme.DangerAccentColor;
        private Color WarningAccentColor => Theme.WarningAccentColor;
        private Color HybridAccentColor => Theme.HybridAccentColor;
        private Color ConditionAccentColor => Theme.ConditionAccentColor;
        private Color RewardAccentColor => Theme.RewardAccentColor;
        private Color MoneyAccentColor => Theme.MoneyAccentColor;
        private Color ItemAccentColor => Theme.ItemAccentColor;
        private Color NeutralAccentColor => Theme.NeutralAccentColor;
        private Color StrongDividerColor => Theme.StrongDividerColor;
        private Color SectionDividerColor => Theme.SectionDividerColor;
        private Color SoftDividerColor => Theme.SoftDividerColor;
        private Color SoftestDividerColor => Theme.SoftestDividerColor;
        private Color GetSelectionOverlayColor(bool isHovered) => Theme.GetSelectionOverlayColor(isHovered);

    }
}
