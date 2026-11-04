using System.Collections.Generic;
using System.Linq;
using EditorTools;
using StateMachine.Graph.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateMachine.Graph.Editor
{
    public partial class StateMachineEditorWindow : GraphEditorWindowBase, IStateMachineNodeInspectorHost
    {
        private const string StatesPathKey = "StateMachineEditor_StatesPath";
        private const string TransitionsPathKey = "StateMachineEditor_TransitionsPath";
        private const string ThemeKey = "StateMachineEditor_Theme";
        private const float WorkspaceWidth = 10000f;
        private const float WorkspaceHeight = 10000f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 2f;
        private const float OverlayPanelWidth = 320f;
        private static readonly Vector2 NodeSize = new(320f, 220f);

        private StateMachineGraph currentGraph;
        private string statesFolderPath;
        private string transitionsFolderPath;
        private readonly Dictionary<Node, Rect> nodeRects = new();
        private readonly Dictionary<State, Node> stateToNodeLookup = new();
        private readonly GraphEditorNodeLayoutSynchronizer<Node> nodeLayoutSynchronizer = new();
        private readonly StateMachineGraphConnectionPresenter connectionPresenter = new();
        private readonly HashSet<Node> expandedNodes = new();

        private bool isSelectingTargetNode;
        private readonly GraphEditorOverlayPanelState controlsPanel = new();
        private bool graphStructureDirty = true;
        private bool graphValidationDirty = true;
        private IReadOnlyList<GraphEditorValidationIssue> cachedGraphValidationIssues;
        private Transition pendingTransition;
        private Node sourceNodeForSelection;
        private Node activeConnectionNode;
        private GraphEditorTheme theme;
        private StateMachineNodeInspector nodeInspector;

        private RetainedGraphCanvas<Node, Transition> toolkitCanvas;
        private DelegatingRetainedGraphCanvasAdapter<Node, Transition> canvasAdapter;
        private IMGUIContainer toolkitControls;

        private GraphEditorTheme Theme => theme ??= new GraphEditorTheme(() => useLightTheme);
        private StateMachineNodeInspector NodeInspector => nodeInspector ??= new StateMachineNodeInspector(this);

        [MenuItem("Tools/State Machine Editor")]
        public static void Open()
        {
            GetWindow<StateMachineEditorWindow>("State Machine Editor");
        }

        private void CreateGUI()
        {
            canvasAdapter = CreateCanvasAdapter();
            toolkitCanvas = new RetainedGraphCanvas<Node, Transition>(
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter);
            toolkitControls = GraphEditorWindowUi.BuildRoot(
                rootVisualElement,
                toolkitCanvas,
                "state-machine-editor-controls",
                OverlayPanelWidth,
                WindowBackgroundColor,
                DrawToolkitControls);

            UpdateControlsInputWidth();
            toolkitCanvas.RebuildNow();
        }

        private void OnEnable()
        {
            statesFolderPath = GraphEditorPreferences.LoadFolder(StatesPathKey, "Assets/States");
            transitionsFolderPath = GraphEditorPreferences.LoadFolder(TransitionsPathKey, "Assets/Transitions");
            useLightTheme = GraphEditorPreferences.LoadTheme(ThemeKey);
            GraphEditorAssetChangeTracker.AssetsChanged += HandleTrackedAssetsChanged;
        }

        private void OnDisable()
        {
            GraphEditorAssetChangeTracker.AssetsChanged -= HandleTrackedAssetsChanged;
            theme?.Dispose();
            theme = null;
            nodeInspector = null;
            toolkitCanvas = null;
            canvasAdapter = null;
            toolkitControls = null;
        }

        private void HandleTrackedAssetsChanged(IReadOnlyCollection<string> changedAssetPaths)
        {
            if (!HasRelevantAssetChange(changedAssetPaths))
            {
                return;
            }

            InvalidateConnectionRouteCache();
            graphValidationDirty = true;
            bool graphAssetChanged = currentGraph != null &&
                changedAssetPaths.Contains(AssetDatabase.GetAssetPath(currentGraph));
            RefreshToolkitCanvas(graphAssetChanged || HasMissingStateReference());
        }

        private bool HasRelevantAssetChange(IReadOnlyCollection<string> changedAssetPaths)
        {
            return GraphEditorAssetChangeFilter.HasRelevantChange(
                changedAssetPaths,
                currentGraph != null ? AssetDatabase.GetAssetPath(currentGraph) : null,
                statesFolderPath,
                transitionsFolderPath);
        }

        private bool HasMissingStateReference()
        {
            return currentGraph != null && currentGraph.Nodes.Any(node =>
                node == null ||
                node.State == null ||
                !AssetDatabase.Contains(node.State) ||
                (node.State.Transitions != null && node.State.Transitions.Any(transition =>
                    transition == null || !AssetDatabase.Contains(transition))));
        }

        private void DrawToolkitControls()
        {
            using (BeginThemedGuiScope())
            {
                DrawControlsOverlay();
            }
        }

        private void DrawToolkitNode(Node node)
        {
            using (BeginThemedGuiScope())
            {
                DrawNodeWindow(node);
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

        private DelegatingRetainedGraphCanvasAdapter<Node, Transition> CreateCanvasAdapter()
        {
            return new DelegatingRetainedGraphCanvasAdapter<Node, Transition>(
                "Create or load a state machine graph.",
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
            stateToNodeLookup.Clear();
            connectionPresenter.ClearNodePresentationState();
        }

        IEnumerable<Node> GetRetainedGraphNodes()
        {
            return currentGraph?.Nodes ?? Enumerable.Empty<Node>();
        }

        IEnumerable<RetainedGraphConnection<Node, Transition>>
            GetRetainedGraphConnections()
        {
            if (currentGraph == null)
            {
                yield break;
            }

            foreach (Node sourceNode in currentGraph.Nodes)
            {
                foreach (RetainedGraphConnection<Node, Transition> connection in GetRetainedGraphConnectionsFrom(sourceNode))
                {
                    yield return connection;
                }
            }
        }

        IEnumerable<RetainedGraphConnection<Node, Transition>> GetRetainedGraphConnectionsFrom(Node sourceNode)
        {
            if (sourceNode?.State == null)
            {
                yield break;
            }

            EnsureCollections(sourceNode.State);
            foreach (Transition transition in sourceNode.State.Transitions)
            {
                if (transition?.TargetState != null &&
                    stateToNodeLookup.TryGetValue(transition.TargetState, out Node targetNode))
                {
                    yield return new RetainedGraphConnection<Node, Transition>(sourceNode, targetNode, transition);
                }
            }
        }

        Vector2 GetRetainedNodePosition(Node node)
        {
            return node.Position;
        }

        void SetRetainedNodePosition(Node node, Vector2 position)
        {
            node.Position = position;
        }

        void SetRetainedNodeRect(Node node, Rect rect)
        {
            nodeRects[node] = rect;
        }

        string GetRetainedNodeTitle(Node node)
        {
            return GetNodeTitle(node);
        }

        Color GetRetainedNodeTint(Node node)
        {
            return GetNodeTint(node);
        }

        bool IsRetainedNodeTargetable(Node node)
        {
            return node != null && node != sourceNodeForSelection && node.State != null;
        }

        void DrawRetainedNode(Node node)
        {
            DrawToolkitNode(node);
        }

        void DeleteRetainedNode(Node node)
        {
            DeleteNode(node, false);
        }

        void SelectRetainedNode(Node node)
        {
            if (activeConnectionNode == node)
            {
                return;
            }

            activeConnectionNode = node;
        }

        void ClearRetainedNodeSelection()
        {
            activeConnectionNode = null;
        }

        bool TrySelectRetainedTarget(Node node)
        {
            if (!isSelectingTargetNode || pendingTransition == null ||
                node == null || node == sourceNodeForSelection || node.State == null)
            {
                return false;
            }

            pendingTransition.TargetState = node.State;
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
            Transition transition,
            Node sourceNode,
            Node targetNode,
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
                activeConnectionNode != null &&
                (ReferenceEquals(sourceNode, activeConnectionNode) || ReferenceEquals(targetNode, activeConnectionNode)),
                nodeRects,
                GetConnectionColor(sourceNode, targetNode));
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

            const float buttonHeight = 28f;
            float y = padding;

            EditorGUI.DrawRect(panelRect, PanelBackgroundColor);
            GUI.Box(panelRect, GUIContent.none, HelpBoxStyle);
            GUILayout.BeginArea(panelRect, GUIContent.none, HelpBoxStyle);
            float contentWidth = OverlayPanelWidth - padding * 2f;

            EditorGUI.LabelField(new Rect(padding, padding, contentWidth, 18f), "States Folder Path:");
            y += 18f;

            statesFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), statesFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for States", ref statesFolderPath, StatesPathKey);
            }

            if (DrawButton(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                GraphEditorPreferences.SaveFolder(StatesPathKey, statesFolderPath);
            }

            y += 28f;

            EditorGUI.LabelField(new Rect(padding, y, contentWidth, 18f), "Transitions Folder Path:");
            y += 18f;

            transitionsFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), transitionsFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Transitions", ref transitionsFolderPath, TransitionsPathKey);
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
            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "New State"))
            {
                CreateNewState();
            }

            EditorGUI.EndDisabledGroup();
            y += buttonHeight + spacing;

            EditorGUI.BeginDisabledGroup(currentGraph == null);
            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Bring Nodes Into View"))
            {
                BringNodesIntoView();
            }

            EditorGUI.EndDisabledGroup();
            y += buttonHeight + spacing;

            if (currentGraph != null)
            {
                State startState = GetStartState();
                if (startState == null)
                {
                    EditorGUI.HelpBox(
                        new Rect(padding, y, contentWidth, 40f),
                        "Start state is not defined. The state machine will not work without it.",
                        MessageType.Warning);
                    y += 46f;
                }

                EditorGUI.BeginDisabledGroup(startState == null);
                if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Ping Start State"))
                {
                    EditorGUIUtility.PingObject(startState);
                    Selection.activeObject = startState;
                }

                EditorGUI.EndDisabledGroup();
                y += buttonHeight + spacing;

                DrawValidationSummary(padding, ref y, contentWidth);
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
            if (toolkitControls != null)
            {
                controlsPanel.ApplyInputWidth(toolkitControls, OverlayPanelWidth);
            }
        }

        private void DrawValidationSummary(float x, ref float y, float width)
        {
            IReadOnlyList<GraphEditorValidationIssue> issues = GetGraphValidationIssues();
            if (issues.Count == 0)
            {
                return;
            }

            int errorCount = issues.Count(issue => issue.Severity == GraphEditorValidationSeverity.Error);
            MessageType type = errorCount > 0 ? MessageType.Error : MessageType.Warning;
            string summary = errorCount > 0
                ? $"Graph validation: {errorCount} error(s), {issues.Count - errorCount} warning(s)."
                : $"Graph validation: {issues.Count} warning(s).";
            EditorGUI.HelpBox(new Rect(x, y, width, 40f), summary, type);
            y += 46f;
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
            currentGraph = StateMachineGraphEditorCommands.CreateGraph();
            RefreshToolkitCanvas(true);
        }

        private void LoadGraph()
        {
            if (!GraphEditorAssetService.TryLoadAsset("Load State Machine Graph", out currentGraph))
            {
                return;
            }

            EnsureGraphNodes();
            graphStructureDirty = true;
            RefreshToolkitCanvas(true);
        }

        private void CreateNewState()
        {
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog(
                    "No Graph Selected",
                    "Please create or load a state machine graph first.",
                    "OK");
                return;
            }

            EnsureGraphNodes();

            if (StateMachineGraphEditorCommands.TryCreateState(
                    currentGraph, statesFolderPath, GetCenteredNodePosition(NodeSize), out _))
            {
                MarkDirty(currentGraph);
                RefreshToolkitCanvas(true);
            }

        }

        private void SynchronizeNodeRects()
        {
            GraphEditorNodeLookup.Rebuild(currentGraph.Nodes, stateToNodeLookup, node => node.State);

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

            bool graphChanged = StateGraphStructureOperations.EnsureNodes(currentGraph);
            graphChanged |= StateGraphStructureOperations.RemoveMissingNodes(currentGraph);

            if (graphChanged)
            {
                MarkDirty(currentGraph);
            }
        }

        private Color GetConnectionColor(Node sourceNode, Node targetNode)
        {
            return GraphEditorConnectionPresentation.GetColor(
                sourceNode,
                targetNode,
                activeConnectionNode,
                PrimaryConnectionColor,
                SourceHighlightConnectionColor,
                TargetHighlightConnectionColor);
        }

    }

    public partial class StateMachineEditorWindow
    {

        private void DrawNodeWindow(Node node)
        {
            EditorGUI.BeginDisabledGroup(isSelectingTargetNode);

            if (!expandedNodes.Contains(node))
            {
                DrawCollapsedNodeSummary(node);
                EditorGUI.EndDisabledGroup();
                return;
            }

            NodeInspector.Draw(node);

            if (node.State == null)
            {
                if (DrawButton("Collapse"))
                {
                    SetNodeExpanded(node, false);
                }

                EditorGUI.EndDisabledGroup();
                return;
            }

            if (DrawButton(IsStartNode(node) ? "Start State" : "Set As Start"))
            {
                MoveNodeToFront(node);
            }

            if (IsOrphanState(node.State))
            {
                EditorGUILayout.HelpBox(
                    "This state has no incoming transitions and is not the start state.",
                    MessageType.Warning);
            }

            if (DrawButton("Collapse"))
            {
                SetNodeExpanded(node, false);
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawCollapsedNodeSummary(Node node)
        {
            NodeInspector.DrawCollapsed(node);

            if (DrawButton("Expand"))
            {
                SetNodeExpanded(node, true);
            }
        }

        private void SetNodeExpanded(Node node, bool isExpanded)
        {
            if (node == null || (isExpanded ? !expandedNodes.Add(node) : !expandedNodes.Remove(node)))
            {
                return;
            }

            if (connectionPresenter.ClearTransitionPickers(node.State))
            {
                toolkitCanvas?.RequestConnectionRepaint();
            }

            toolkitCanvas?.RequestNodeContentRepaint(node);
        }

        private void BringNodesIntoView()
        {
            if (currentGraph?.Nodes == null || toolkitCanvas == null)
            {
                return;
            }

            Rect accessibleViewport = toolkitCanvas.VisibleGraphRect;
            float zoomFactor = Mathf.Max(zoom, 0.0001f);
            float controlsWidth = controlsPanel.IsExpanded
                ? OverlayPanelWidth
                : GraphEditorOverlayPanelState.CollapsedInputWidth;
            accessibleViewport.xMin += controlsWidth / zoomFactor + 12f;
            accessibleViewport.xMax -= 12f;
            accessibleViewport.yMin += 12f;
            accessibleViewport.yMax -= 12f;

            int recoveredCount = GraphEditorNodePositionRecovery.ClampToViewport(
                currentGraph.Nodes,
                node => node.Position,
                (node, position) => node.Position = position,
                accessibleViewport,
                NodeSize,
                24f);
            if (recoveredCount == 0)
            {
                return;
            }

            MarkDirty(currentGraph);
            RefreshToolkitCanvas(true);
        }

        private void RemoveTransition(State state, int removeTransitionIndex)
        {
            Transition removedTransition = StateMachineGraphEditorCommands.RemoveTransitionReference(state, removeTransitionIndex);
            MarkDirty(state);

            if (removedTransition != null && StateGraphReferenceOperations.CanDeleteTransitionAsset(removedTransition))
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
                        GraphEditorAssetService.DeleteAsset(removedTransition, "Delete state transition");
                    }
                }
            }

            GraphEditorAssetService.FlushChanges();
            Selection.activeObject = null;
            GUIUtility.ExitGUI();
        }

        private void DeleteNode(Node node, bool exitGui = true)
        {
            expandedNodes.Remove(node);
            bool shouldDeleteStateAsset = node.State != null &&
                                          EditorUtility.DisplayDialog(
                                              "Delete State?",
                                              $"Do you want to delete the state \"{node.State.name}\" from the project?",
                                              "Yes",
                                              "No");

            if (node.State != null)
            {
                RemoveStateReferences(node.State);

                if (shouldDeleteStateAsset)
                {
                    StateGraphReferenceOperations.DeleteUnsharedTransitions(node.State);

                    GraphEditorAssetService.DeleteAsset(node.State, "Delete state");
                }
            }

            StateMachineGraphEditorCommands.RemoveNode(currentGraph, node);
            MarkDirty(currentGraph);
            GraphEditorAssetService.FlushChanges();
            RefreshToolkitCanvas(true);
            if (exitGui)
            {
                GUIUtility.ExitGUI();
            }
        }

        private void RemoveStateReferences(State state)
        {
            StateGraphReferenceOperations.RemoveIncomingReferences(currentGraph, state);

            if ((sourceNodeForSelection != null && sourceNodeForSelection.State == state) ||
                (pendingTransition != null && pendingTransition.TargetState == state))
            {
                CancelTargetSelection();
            }
        }

        private void ReplaceStateReferences(State oldState, State newState)
        {
            StateGraphReferenceOperations.ReplaceIncomingReferences(currentGraph, oldState, newState);
        }

        StateMachineGraph IStateMachineNodeInspectorHost.CurrentGraph => currentGraph;
        GraphEditorTheme IStateMachineNodeInspectorHost.Theme => Theme;

        bool IStateMachineNodeInspectorHost.ContainsState(State state) => ContainsState(state);

        bool IStateMachineNodeInspectorHost.TryAssignState(Node node, State state)
        {
            if (state != null && currentGraph.Nodes.Exists(candidate => candidate != node && candidate.State == state))
            {
                EditorUtility.DisplayDialog(
                    "Duplicate State Detected",
                    $"State \"{state.name}\" is already assigned to another node.",
                    "OK");
                return false;
            }

            ReplaceStateReferences(node.State, state);
            node.State = state;
            MarkDirty(currentGraph);
            RefreshToolkitCanvas(true);
            return true;
        }

        void IStateMachineNodeInspectorHost.MarkInspectorAssetDirty(UnityEngine.Object asset) => MarkDirty(asset);

        void IStateMachineNodeInspectorHost.NotifyTransitionTargetChanged(Transition transition)
        {
            InvalidateConnectionRouteCache();
            toolkitCanvas?.RebuildConnectionsGradually();
        }

        void IStateMachineNodeInspectorHost.BeginTransitionTargetSelection(Transition transition, Node sourceNode)
        {
            isSelectingTargetNode = true;
            pendingTransition = transition;
            sourceNodeForSelection = sourceNode;
            RefreshToolkitCanvas();
        }

        void IStateMachineNodeInspectorHost.CreateTransition(State state)
        {
            if (StateMachineGraphEditorCommands.TryCreateTransition(state, transitionsFolderPath))
            {
                MarkDirty(state);
                RefreshToolkitCanvas(true);
            }
        }

        void IStateMachineNodeInspectorHost.RemoveTransition(State state, int index) => RemoveTransition(state, index);

        void IStateMachineNodeInspectorHost.RecordTransitionPicker(Transition transition, Rect pickerRect)
        {
            if (connectionPresenter.RecordTransitionPicker(transition, pickerRect))
            {
                toolkitCanvas?.RequestConnectionRepaint();
            }
        }

        private void EnsureGraphNodes()
        {
            if (StateGraphStructureOperations.EnsureNodes(currentGraph)) MarkDirty(currentGraph);
        }

        private State GetStartState()
        {
            return StateGraphStructureOperations.GetStartState(currentGraph);
        }

        private bool ContainsState(State state)
        {
            return StateGraphStructureOperations.ContainsState(currentGraph, state);
        }

        private bool IsStartNode(Node node)
        {
            return StateGraphStructureOperations.IsStartNode(currentGraph, node);
        }

        private bool IsOrphanState(State state)
        {
            return StateGraphStructureOperations.IsOrphanState(currentGraph, state);
        }

        private void MoveNodeToFront(Node node)
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

        private Color GetNodeTint(Node node)
        {
            if (node.State == null)
            {
                return Color.white;
            }

            if (IsStartNode(node))
            {
                return StartNodeTint;
            }

            if (IsOrphanState(node.State))
            {
                return OrphanNodeTint;
            }

            return Color.white;
        }

        private string GetNodeTitle(Node node)
        {
            if (node.State == null)
            {
                return "State Node";
            }

            string prefix = IsStartNode(node) ? "[Start] " : string.Empty;
            string stateName = string.IsNullOrWhiteSpace(node.State.Name)
                ? node.State.name
                : node.State.Name;
            return prefix + stateName;
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

        private static void EnsureCollections(State state)
        {
            if (state == null)
            {
                return;
            }

            if (state.Behaviours == null)
            {
                state.Behaviours = new List<BaseBehaviour>();
            }

            if (state.Transitions == null)
            {
                state.Transitions = new List<Transition>();
            }
        }

        private void MarkDirty(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            graphValidationDirty = true;
        }

        private IReadOnlyList<GraphEditorValidationIssue> GetGraphValidationIssues()
        {
            if (graphValidationDirty || cachedGraphValidationIssues == null)
            {
                cachedGraphValidationIssues = StateMachineGraphValidator.Validate(currentGraph);
                graphValidationDirty = false;
            }

            return cachedGraphValidationIssues;
        }

        private void MarkNodePositionDirty()
        {
            if (currentGraph != null)
            {
                EditorUtility.SetDirty(currentGraph);
            }
        }

        private string GetThemeToggleLabel() => Theme.GetThemeToggleLabel();

        private void DrawWindowBackground() =>
            Theme.DrawWindowBackground(new Rect(0f, 0f, position.width, position.height));

        protected override void ApplyThemedGuiState() => Theme.ApplyGuiState();

        protected override void RestoreThemedGuiState() => Theme.RestoreGuiState();

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

        private GUIStyle NodeWindowStyle => Theme.NodeWindowStyle;
        private GUIStyle HelpBoxStyle => Theme.HelpBoxStyle;
        private GUIStyle ButtonStyle => Theme.ButtonStyle;
        private GUIStyle MiniButtonStyle => Theme.MiniButtonStyle;
        private GUIStyle PopupStyle => Theme.PopupStyle;
        private GUIStyle TextFieldStyle => Theme.TextFieldStyle;
        private GUIStyle MiniBoldLabelStyle => Theme.MiniBoldLabelStyle;
        private GUIStyle MiniLabelStyle => Theme.MiniLabelStyle;
        private GUIStyle CenteredMiniLabelStyle => Theme.CenteredMiniLabelStyle;

        private void DrawEnumPropertyField(SerializedProperty property, string label = null)
        {
            if (property == null)
            {
                return;
            }

            if (property.propertyType != SerializedPropertyType.Enum)
            {
                if (string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.PropertyField(property);
                }
                else
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(label));
                }

                return;
            }

            string popupLabel = string.IsNullOrEmpty(label) ? property.displayName : label;
            int selectedIndex = EditorGUILayout.Popup(popupLabel, property.enumValueIndex, property.enumDisplayNames, PopupStyle);
            if (selectedIndex != property.enumValueIndex)
            {
                property.enumValueIndex = selectedIndex;
            }
        }

        private Color PanelBackgroundColor => Theme.PanelBackgroundColor;
        private Color WindowBackgroundColor => Theme.WindowBackgroundColor;
        private Color CanvasBackgroundColor => Theme.CanvasBackgroundColor;
        private Color MinorGridColor => Theme.MinorGridColor;
        private Color MajorGridColor => Theme.MajorGridColor;
        private Color PrimaryConnectionColor => Theme.PrimaryConnectionColor;
        private Color SourceHighlightConnectionColor => Theme.SourceHighlightConnectionColor;
        private Color TargetHighlightConnectionColor => Theme.TargetHighlightConnectionColor;
        private Color DangerButtonColor => Theme.DangerButtonColor;
        private Color LinkButtonColor => Theme.LinkButtonColor;
        private Color StartBadgeColor => Theme.StartBadgeColor;
        private Color WarningBadgeColor => Theme.WarningBadgeColor;
        private Color MissingLinkPanelTint => Theme.MissingLinkPanelTint;
        private Color OutsideGraphPanelTint => Theme.OutsideGraphPanelTint;
        private Color StartNodeTint => Theme.StartNodeTint;
        private Color OrphanNodeTint => Theme.OrphanNodeTint;
        private Color SelectionOverlayTextColor => Theme.SelectionOverlayTextColor;
        private Color GetSelectionOverlayColor(bool isHovered) => Theme.GetSelectionOverlayColor(isHovered);
    }
}
