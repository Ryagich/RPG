using System.Collections.Generic;
using System.IO;
using System.Linq;
using EditorTools;
using StateMachine.Graph.Model;
using UnityEditor;
using UnityEngine;

namespace StateMachine.Graph.Editor
{
    public class StateMachineEditorWindow : EditorWindow
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
        private Vector2 scrollPos;
        private string statesFolderPath;
        private string transitionsFolderPath;
        private readonly Dictionary<Transition, Vector2> transitionAnchorPositions = new();
        private readonly Dictionary<Node, Rect> nodeRects = new();
        private readonly Dictionary<State, Node> stateToNodeLookup = new();
        private readonly Dictionary<Transition, CachedConnectionRoute> connectionRouteCache = new();
        private readonly HashSet<Node> graphNodeSet = new();
        private readonly List<Node> staleNodeRects = new();

        private bool isSelectingTargetNode;
        private bool isControlsPanelExpanded = true;
        private bool graphStructureDirty = true;
        private int connectionLayoutVersion;
        private Transition pendingTransition;
        private Node sourceNodeForSelection;
        private Node activeConnectionNode;
        private readonly List<EditorStyleTextOverride> editorStyleTextOverrides = new();
        private GUIStyle lightWindowStyle;
        private GUIStyle lightHelpBoxStyle;
        private GUIStyle lightButtonStyle;
        private GUIStyle lightMiniButtonStyle;
        private GUIStyle lightPopupStyle;
        private GUIStyle lightTextFieldStyle;
        private GUIStyle lightMiniBoldLabelStyle;
        private GUIStyle lightMiniLabelStyle;
        private GUIStyle lightCenteredMiniLabelStyle;
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

        [MenuItem("Tools/State Machine Editor")]
        public static void Open()
        {
            GetWindow<StateMachineEditorWindow>("State Machine Editor");
        }

        private void OnEnable()
        {
            statesFolderPath = EditorPrefs.GetString(StatesPathKey, "Assets/States");
            transitionsFolderPath = EditorPrefs.GetString(TransitionsPathKey, "Assets/Transitions");
            useLightTheme = EditorPrefs.GetBool(ThemeKey, false);
            EditorApplication.projectChanged += HandleProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
        }

        private void HandleProjectChanged()
        {
            graphStructureDirty = true;
            InvalidateConnectionRouteCache();
            Repaint();
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
                "Create or load a state machine graph.");
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

            EditorGUI.LabelField(new Rect(padding, padding, contentWidth, 18f), "States Folder Path:");
            y += 18f;

            statesFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), statesFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for States", ref statesFolderPath, StatesPathKey);
            }

            if (DrawButton(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                EditorPrefs.SetString(StatesPathKey, statesFolderPath);
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
            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "New State"))
            {
                CreateNewState();
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
            currentGraph = CreateInstance<StateMachineGraph>();
            ProjectWindowUtil.CreateAsset(currentGraph, "NewStateMachineGraph.asset");
        }

        private void LoadGraph()
        {
            string path = EditorUtility.OpenFilePanel("Load State Machine Graph", "Assets", "asset");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = "Assets" + path.Replace(Application.dataPath, "");
            currentGraph = AssetDatabase.LoadAssetAtPath<StateMachineGraph>(path);
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog("Invalid Asset", "Selected asset is not a StateMachineGraph.", "OK");
                return;
            }

            EnsureGraphNodes();
            graphStructureDirty = true;
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

            if (!EnsureFolderExists(statesFolderPath, "Please specify the folder for saving states."))
            {
                return;
            }

            string fileName = $"State_{currentGraph.Nodes.Count}.asset";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(statesFolderPath, fileName));

            var state = CreateInstance<State>();
            state.name = Path.GetFileNameWithoutExtension(targetPath);

            AssetDatabase.CreateAsset(state, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var newNode = new Node(state)
            {
                Position = GetCenteredNodePosition(NodeSize)
            };

            currentGraph.Nodes.Add(newNode);
            MarkDirty(currentGraph);

            EditorGUIUtility.PingObject(state);
            Selection.activeObject = state;
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
            if (graphStructureDirty)
            {
                CleanupGraph();
                graphStructureDirty = false;
            }

            SynchronizeNodeRects();
            transitionAnchorPositions.Clear();

            Event currentEvent = Event.current;
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

            EditorGUI.BeginDisabledGroup(isSelectingTargetNode);
            BeginWindows();

            for (int i = 0; i < currentGraph.Nodes.Count; i++)
            {
                Node node = currentGraph.Nodes[i];
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
                rect = GUILayout.Window(i, rect, _ => DrawNodeWindow(node), GetNodeTitle(node), NodeWindowStyle);
                GUI.color = previousColor;

                nodeRects[node] = rect;
                node.Position = rect.position;
                if (!RectApproximatelyEqual(previousRect, rect))
                {
                    InvalidateConnectionRouteCache();
                }
            }

            EndWindows();
            HandleConnectionHighlightSelection(currentEvent);
            EditorGUI.EndDisabledGroup();

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
            stateToNodeLookup.Clear();
            bool layoutChanged = false;

            foreach (Node node in currentGraph.Nodes)
            {
                if (node == null)
                {
                    continue;
                }

                graphNodeSet.Add(node);
                if (node.State != null)
                {
                    stateToNodeLookup[node.State] = node;
                }

                if (!nodeRects.TryGetValue(node, out Rect rect))
                {
                    nodeRects[node] = new Rect(node.Position, NodeSize);
                    layoutChanged = true;
                    continue;
                }

                if (rect.position != node.Position)
                {
                    rect.position = node.Position;
                    nodeRects[node] = rect;
                    layoutChanged = true;
                }
            }

            foreach (Node node in nodeRects.Keys)
            {
                if (!graphNodeSet.Contains(node))
                {
                    staleNodeRects.Add(node);
                }
            }

            foreach (Node node in staleNodeRects)
            {
                nodeRects.Remove(node);
                layoutChanged = true;
            }

            if (layoutChanged)
            {
                InvalidateConnectionRouteCache();
            }
        }

        private void InvalidateConnectionRouteCache()
        {
            connectionRouteCache.Clear();
            connectionLayoutVersion++;
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
                Node node = currentGraph.Nodes[i];
                if (node == null)
                {
                    currentGraph.Nodes.RemoveAt(i);
                    graphChanged = true;
                    continue;
                }

                if (node.State == null || !AssetDatabase.Contains(node.State))
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

        private void DrawNodeMarkers(Rect visibleGraphRect)
        {
            foreach (KeyValuePair<Node, Rect> pair in nodeRects)
            {
                Node node = pair.Key;
                if (node.State == null)
                {
                    continue;
                }

                if (!GraphEditorCanvasUtility.IsAtLeastPartiallyVisible(pair.Value, visibleGraphRect))
                {
                    continue;
                }

                Rect badgeRect = new Rect(pair.Value.x + 6f, pair.Value.y + 6f, 18f, 18f);
                Color previous = GUI.backgroundColor;

                if (IsStartNode(node))
                {
                    GUI.backgroundColor = StartBadgeColor;
                    GUI.Box(badgeRect, "S");
                }
                else if (IsOrphanState(node.State))
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

            foreach (Node node in currentGraph.Nodes)
            {
                State state = node.State;
                if (state == null)
                {
                    continue;
                }

                EnsureCollections(state);

                foreach (Transition transition in state.Transitions)
                {
                    if (transition == null || transition.TargetState == null)
                    {
                        continue;
                    }

                    if (!stateToNodeLookup.TryGetValue(transition.TargetState, out Node targetNode))
                    {
                        continue;
                    }

                    if (!nodeRects.TryGetValue(node, out Rect sourceRect) ||
                        !nodeRects.TryGetValue(targetNode, out Rect targetRect))
                    {
                        continue;
                    }

                    Vector2 startPos;
                    if (!transitionAnchorPositions.TryGetValue(transition, out startPos))
                    {
                        startPos = new Vector2(sourceRect.xMax - 12f, sourceRect.center.y);
                    }

                    Handles.color = GetConnectionColor(node, targetNode);
                    Vector2[] routePoints = GetOrBuildConnectionRoute(transition, startPos, sourceRect, targetRect);
                    DrawSmoothedConnection(routePoints);
                    DrawConnectionArrow(routePoints);
                }
            }

            Handles.EndGUI();
        }

        private Vector2[] GetOrBuildConnectionRoute(
            Transition transition,
            Vector2 startPos,
            Rect sourceRect,
            Rect targetRect)
        {
            if (connectionRouteCache.TryGetValue(transition, out CachedConnectionRoute cachedRoute) &&
                cachedRoute.LayoutVersion == connectionLayoutVersion &&
                ApproximatelyEqual(cachedRoute.StartPos, startPos) &&
                RectApproximatelyEqual(cachedRoute.SourceRect, sourceRect) &&
                RectApproximatelyEqual(cachedRoute.TargetRect, targetRect))
            {
                return cachedRoute.RoutePoints;
            }

            Vector2[] routePoints = BuildConnectionRoute(startPos, sourceRect, targetRect, nodeRects.Values);
            connectionRouteCache[transition] = new CachedConnectionRoute(
                connectionLayoutVersion,
                startPos,
                sourceRect,
                targetRect,
                routePoints);
            return routePoints;
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

        private Color GetConnectionColor(Node sourceNode, Node targetNode)
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

        private Vector2 GetGraphMousePosition(Vector2 mousePosition)
        {
            return (mousePosition - panOffset) / zoom;
        }

        private static Vector2[] BuildConnectionRoute(Vector2 startPos, Rect sourceRect, Rect targetRect, IEnumerable<Rect> allNodeRects)
        {
            const float clearance = 28f;

            ConnectionPort startPort = GetSourcePort(startPos, sourceRect, targetRect, clearance);
            ConnectionPort endPort = GetTargetPort(sourceRect, targetRect, clearance);

            List<Rect> obstacles = allNodeRects
                .Select(rect => ExpandRect(rect, clearance))
                .ToList();

            List<Vector2> routedPoints = FindOrthogonalPath(startPort.OuterPoint, endPort.OuterPoint, obstacles);
            if (routedPoints == null || routedPoints.Count == 0)
            {
                return SimplifyRoute(new[]
                {
                    startPos,
                    startPort.OuterPoint,
                    endPort.OuterPoint,
                    endPort.EdgePoint
                });
            }

            var fullRoute = new List<Vector2>(routedPoints.Count + 3) { startPos };
            if (!ApproximatelyEqual(startPos, startPort.OuterPoint))
            {
                fullRoute.Add(startPort.OuterPoint);
            }

            for (int i = 1; i < routedPoints.Count; i++)
            {
                fullRoute.Add(routedPoints[i]);
            }

            if (!ApproximatelyEqual(fullRoute[fullRoute.Count - 1], endPort.OuterPoint))
            {
                fullRoute.Add(endPort.OuterPoint);
            }

            if (!ApproximatelyEqual(endPort.OuterPoint, endPort.EdgePoint))
            {
                fullRoute.Add(endPort.EdgePoint);
            }

            return SimplifyRoute(fullRoute);
        }

        private static void DrawConnectionArrow(IReadOnlyList<Vector2> routePoints)
        {
            if (routePoints == null || routePoints.Count < 2)
            {
                return;
            }

            Vector2 arrowTip = routePoints[routePoints.Count - 1];
            Vector2 previousPoint = routePoints[routePoints.Count - 2];
            Vector2 direction = (arrowTip - previousPoint).normalized;
            if (direction.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 arrowBase1 = arrowTip - direction * 13f + perpendicular * 5.5f;
            Vector2 arrowBase2 = arrowTip - direction * 13f - perpendicular * 5.5f;
            Handles.DrawAAConvexPolygon(arrowTip, arrowBase1, arrowBase2);
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

        private readonly struct CachedConnectionRoute
        {
            public CachedConnectionRoute(int layoutVersion, Vector2 startPos, Rect sourceRect, Rect targetRect, Vector2[] routePoints)
            {
                LayoutVersion = layoutVersion;
                StartPos = startPos;
                SourceRect = sourceRect;
                TargetRect = targetRect;
                RoutePoints = routePoints;
            }

            public int LayoutVersion { get; }
            public Vector2 StartPos { get; }
            public Rect SourceRect { get; }
            public Rect TargetRect { get; }
            public Vector2[] RoutePoints { get; }
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
            if (!isSelectingTargetNode || pendingTransition == null)
            {
                return;
            }

            Handles.BeginGUI();

            foreach (Node node in currentGraph.Nodes)
            {
                if (node == sourceNodeForSelection || node.State == null)
                {
                    continue;
                }

                if (!nodeRects.TryGetValue(node, out Rect rect) ||
                    !GraphEditorCanvasUtility.IsAtLeastPartiallyVisible(rect, visibleGraphRect))
                {
                    continue;
                }

                bool isHovered = rect.Contains(Event.current.mousePosition);
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
                    rect.Contains(Event.current.mousePosition))
                {
                    pendingTransition.TargetState = node.State;
                    MarkDirty(pendingTransition);
                    CancelTargetSelection(false);
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            Handles.EndGUI();
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

        private void DrawNodeWindow(Node node)
        {
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
            var newState = (State)EditorGUILayout.ObjectField(node.State, typeof(State), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (newState != null && currentGraph.Nodes.Exists(n => n != node && n.State == newState))
                {
                    EditorUtility.DisplayDialog(
                        "Duplicate State Detected",
                        $"State \"{newState.name}\" is already assigned to another node.",
                        "OK");
                }
                else
                {
                    ReplaceStateReferences(node.State, newState);
                    node.State = newState;
                    MarkDirty(currentGraph);
                }
            }

            if (node.State == null)
            {
                EditorGUILayout.HelpBox("No state assigned.", MessageType.Warning);
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                EditorGUI.EndDisabledGroup();
                return;
            }

            EnsureCollections(node.State);
            DrawStateEditor(node.State, node);

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

            EditorGUI.EndDisabledGroup();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawStateEditor(State state, Node ownerNode)
        {
            SerializedObject stateObject = new SerializedObject(state);
            stateObject.Update();

            SerializedProperty nameProperty = stateObject.FindProperty("<Name>k__BackingField");
            if (nameProperty != null)
            {
                EditorGUILayout.PropertyField(nameProperty, new GUIContent("Name"), true);
            }

            if (stateObject.hasModifiedProperties)
            {
                stateObject.ApplyModifiedProperties();
                MarkDirty(state);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Behaviours", MiniBoldLabelStyle);
            DrawBehavioursSection(state);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Transitions", MiniBoldLabelStyle);
            DrawTransitionsSection(state, ownerNode);
        }

        private void DrawBehavioursSection(State state)
        {
            EnsureCollections(state);

            int removeBehaviourIndex = -1;

            for (int i = 0; i < state.Behaviours.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                var newBehaviour = (BaseBehaviour)EditorGUILayout.ObjectField(
                    state.Behaviours[i],
                    typeof(BaseBehaviour),
                    false,
                    GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    state.Behaviours[i] = newBehaviour;
                    MarkDirty(state);
                }

                if (DrawMiniButton("X", GUILayout.Width(22f)))
                {
                    removeBehaviourIndex = i;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (state.Behaviours.Count == 0)
            {
                EditorGUILayout.LabelField("- none -", MiniLabelStyle);
            }

            if (DrawButton("+ Add Behaviour"))
            {
                state.Behaviours.Add(null);
                MarkDirty(state);
            }

            if (removeBehaviourIndex >= 0)
            {
                state.Behaviours.RemoveAt(removeBehaviourIndex);
                MarkDirty(state);
            }
        }

        private void DrawTransitionsSection(State state, Node ownerNode)
        {
            EnsureCollections(state);
            CleanupMissingTransitions(state);

            int removeTransitionIndex = -1;

            for (int i = 0; i < state.Transitions.Count; i++)
            {
                Transition transition = state.Transitions[i];
                if (transition == null || !AssetDatabase.Contains(transition))
                {
                    continue;
                }

                bool missingLink = transition.TargetState == null;
                bool targetOutsideGraph = transition.TargetState != null && !ContainsState(transition.TargetState);

                Color previousColor = GUI.color;
                if (missingLink)
                {
                    GUI.color = MissingLinkPanelTint;
                }
                else if (targetOutsideGraph)
                {
                    GUI.color = OutsideGraphPanelTint;
                }

                EditorGUILayout.BeginVertical(HelpBoxStyle);
                GUI.color = previousColor;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Transition {i + 1}", MiniBoldLabelStyle);

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

                EditorGUILayout.EndHorizontal();

                DrawTransitionAssetField(state, i);

                transition = state.Transitions[i];
                if (transition != null)
                {
                    DrawTransitionInspector(transition);

                    if (transition.TargetState == null)
                    {
                        EditorGUILayout.HelpBox("Target state is not assigned for this transition.", MessageType.Error);
                    }
                    else if (!ContainsState(transition.TargetState))
                    {
                        EditorGUILayout.HelpBox("Target state is not added to the current graph.", MessageType.Warning);
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }

            if (DrawButton("+ Add Transition"))
            {
                CreateTransitionAsset(state);
            }

            if (removeTransitionIndex >= 0 && removeTransitionIndex < state.Transitions.Count)
            {
                RemoveTransition(state, removeTransitionIndex);
            }
        }

        private void DrawTransitionAssetField(State state, int index)
        {
            Transition transition = state.Transitions[index];

            EditorGUI.BeginChangeCheck();
            var newTransition = (Transition)EditorGUILayout.ObjectField("Asset", transition, typeof(Transition), false);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            if (newTransition == null)
            {
                state.Transitions[index] = null;
                MarkDirty(state);
                return;
            }

            bool duplicateInOtherState = currentGraph.Nodes.Any(node =>
                node.State != null &&
                node.State != state &&
                node.State.Transitions != null &&
                node.State.Transitions.Contains(newTransition));

            bool duplicateInSameState = false;
            for (int i = 0; i < state.Transitions.Count; i++)
            {
                if (i != index && state.Transitions[i] == newTransition)
                {
                    duplicateInSameState = true;
                    break;
                }
            }

            if (duplicateInOtherState)
            {
                EditorUtility.DisplayDialog(
                    "Duplicate Transition Detected",
                    $"Transition \"{newTransition.name}\" is already used in another state.",
                    "OK");
                return;
            }

            if (duplicateInSameState)
            {
                EditorUtility.DisplayDialog(
                    "Duplicate Transition Detected",
                    $"Transition \"{newTransition.name}\" already exists in this state.",
                    "OK");
                return;
            }

            state.Transitions[index] = newTransition;
            MarkDirty(state);
        }

        private void DrawTransitionInspector(Transition transition)
        {
            SerializedObject transitionObject = new SerializedObject(transition);
            transitionObject.Update();

            SerializedProperty typeProperty = transitionObject.FindProperty("<Type>k__BackingField");
            SerializedProperty conditionsProperty = transitionObject.FindProperty("<Conditions>k__BackingField");
            SerializedProperty actionsProperty = transitionObject.FindProperty("<ActionOnTransitions>k__BackingField");
            SerializedProperty targetStateProperty = transitionObject.FindProperty("TargetState");

            if (typeProperty != null)
            {
                DrawEnumPropertyField(typeProperty);
            }

            if (targetStateProperty != null)
            {
                EditorGUILayout.PropertyField(targetStateProperty);
            }

            if (conditionsProperty != null)
            {
                EditorGUILayout.PropertyField(conditionsProperty, true);
            }

            if (actionsProperty != null)
            {
                EditorGUILayout.PropertyField(actionsProperty, true);
            }

            if (transitionObject.hasModifiedProperties)
            {
                transitionObject.ApplyModifiedProperties();
                MarkDirty(transition);
            }
        }

        private void CleanupMissingTransitions(State state)
        {
            for (int i = state.Transitions.Count - 1; i >= 0; i--)
            {
                Transition transition = state.Transitions[i];
                if (transition == null || !AssetDatabase.Contains(transition))
                {
                    state.Transitions.RemoveAt(i);
                    MarkDirty(state);
                }
            }
        }

        private void CreateTransitionAsset(State state)
        {
            if (!EnsureFolderExists(transitionsFolderPath, "Please specify the folder for saving transitions."))
            {
                return;
            }

            string fileName = $"{state.name}_Transition_{state.Transitions.Count}.asset";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(transitionsFolderPath, fileName));

            var newTransition = CreateInstance<Transition>();
            newTransition.name = Path.GetFileNameWithoutExtension(targetPath);

            AssetDatabase.CreateAsset(newTransition, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            state.Transitions.Add(newTransition);
            MarkDirty(state);
        }

        private void RemoveTransition(State state, int removeTransitionIndex)
        {
            Transition removedTransition = state.Transitions[removeTransitionIndex];
            state.Transitions.RemoveAt(removeTransitionIndex);
            MarkDirty(state);

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

        private void DeleteNode(Node node)
        {
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
                    DeleteOwnedTransitions(node.State);

                    string statePath = AssetDatabase.GetAssetPath(node.State);
                    if (!string.IsNullOrEmpty(statePath))
                    {
                        AssetDatabase.DeleteAsset(statePath);
                    }
                }
            }

            currentGraph.Nodes.Remove(node);
            MarkDirty(currentGraph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
        }

        private void RemoveStateReferences(State state)
        {
            foreach (Node otherNode in currentGraph.Nodes)
            {
                if (otherNode.State == null)
                {
                    continue;
                }

                EnsureCollections(otherNode.State);

                foreach (Transition transition in otherNode.State.Transitions)
                {
                    if (transition != null && transition.TargetState == state)
                    {
                        transition.TargetState = null;
                        MarkDirty(transition);
                    }
                }
            }

            if ((sourceNodeForSelection != null && sourceNodeForSelection.State == state) ||
                (pendingTransition != null && pendingTransition.TargetState == state))
            {
                CancelTargetSelection();
            }
        }

        private void ReplaceStateReferences(State oldState, State newState)
        {
            if (oldState == null || oldState == newState)
            {
                return;
            }

            foreach (Node node in currentGraph.Nodes)
            {
                if (node.State == null)
                {
                    continue;
                }

                EnsureCollections(node.State);

                foreach (Transition transition in node.State.Transitions)
                {
                    if (transition != null && transition.TargetState == oldState)
                    {
                        transition.TargetState = newState;
                        MarkDirty(transition);
                    }
                }
            }
        }

        private void DeleteOwnedTransitions(State state)
        {
            EnsureCollections(state);

            foreach (Transition transition in state.Transitions.ToList())
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

            state.Transitions.Clear();
            MarkDirty(state);
        }

        private void EnsureGraphNodes()
        {
            if (currentGraph == null)
            {
                return;
            }

            if (currentGraph.Nodes == null)
            {
                currentGraph.Nodes = new List<Node>();
                MarkDirty(currentGraph);
            }
        }

        private State GetStartState()
        {
            return currentGraph != null &&
                   currentGraph.Nodes.Count > 0 &&
                   currentGraph.Nodes[0] != null
                ? currentGraph.Nodes[0].State
                : null;
        }

        private bool ContainsState(State state)
        {
            return currentGraph.Nodes.Any(node => node.State == state);
        }

        private bool IsStartNode(Node node)
        {
            return currentGraph != null &&
                   currentGraph.Nodes.Count > 0 &&
                   currentGraph.Nodes[0] == node &&
                   node.State != null;
        }

        private bool IsOrphanState(State state)
        {
            if (state == null || GetStartState() == state)
            {
                return false;
            }

            return !currentGraph.Nodes
                .Where(node => node.State != null)
                .SelectMany(node => node.State.Transitions ?? new List<Transition>())
                .Any(transition => transition != null && transition.TargetState == state);
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
            return prefix + node.State.name;
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
            InvalidateConnectionRouteCache();
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

        private GUIStyle MiniBoldLabelStyle => useLightTheme
            ? lightMiniBoldLabelStyle ??= CreateLabelStyle(EditorStyles.miniBoldLabel)
            : EditorStyles.miniBoldLabel;

        private GUIStyle MiniLabelStyle => useLightTheme
            ? lightMiniLabelStyle ??= CreateLabelStyle(EditorStyles.miniLabel, MutedContentColor)
            : EditorStyles.miniLabel;

        private GUIStyle CenteredMiniLabelStyle => useLightTheme
            ? lightCenteredMiniLabelStyle ??= CreateLabelStyle(EditorStyles.centeredGreyMiniLabel, MutedContentColor)
            : EditorStyles.centeredGreyMiniLabel;

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

        private Color MissingLinkPanelTint => useLightTheme
            ? new Color(1f, 0.86f, 0.86f, 1f)
            : new Color(1f, 0.75f, 0.75f, 1f);

        private Color OutsideGraphPanelTint => useLightTheme
            ? new Color(1f, 0.93f, 0.78f, 1f)
            : new Color(1f, 0.90f, 0.65f, 1f);

        private Color StartNodeTint => useLightTheme
            ? new Color(0.84f, 0.95f, 0.84f, 1f)
            : new Color(0.82f, 1f, 0.82f, 1f);

        private Color OrphanNodeTint => useLightTheme
            ? new Color(0.98f, 0.90f, 0.74f, 1f)
            : new Color(1f, 0.92f, 0.72f, 1f);

        private Color SelectionOverlayTextColor => useLightTheme
            ? new Color(0.10f, 0.16f, 0.12f, 1f)
            : Color.white;

        private Color GetSelectionOverlayColor(bool isHovered)
        {
            return useLightTheme
                ? new Color(0.18f, 0.65f, 0.24f, isHovered ? 0.32f : 0.18f)
                : new Color(0f, 0.75f, 0.20f, isHovered ? 0.45f : 0.25f);
        }
    }
}
