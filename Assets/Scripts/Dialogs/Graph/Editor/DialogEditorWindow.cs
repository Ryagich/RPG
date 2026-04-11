using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dialogs.Graph.Model;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Dialogs.Graph.Editor
{
    public class DialogEditorWindow : EditorWindow
    {
        private const string DialogsPathKey = "DialogEditor_DialogsPath";
        private const string PhrasesPathKey = "DialogEditor_PhrasesPath";
        private const float WorkspaceWidth = 10000f;
        private const float WorkspaceHeight = 10000f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 2f;
        private const float OverlayPanelWidth = 320f;

        private DialogGraph currentGraph;
        private Vector2 scrollPos;
        private string dialogsFolderPath;
        private string phrasesFolderPath;
        private readonly Dictionary<DialogAnswer, Vector2> answerAnchorPositions = new();
        private readonly Dictionary<DialogNode, Rect> nodeRects = new();

        private bool isSelectingTargetPhrase;
        private bool isControlsPanelExpanded = true;
        private DialogAnswer pendingAnswer;
        private DialogPhrase sourcePhraseForSelection;

        private float zoom = 1f;
        private Vector2 panOffset = Vector2.zero;

        [MenuItem("Tools/Dialog Editor")]
        public static void Open()
        {
            GetWindow<DialogEditorWindow>("Dialog Editor");
        }

        private void OnEnable()
        {
            dialogsFolderPath = EditorPrefs.GetString(DialogsPathKey, "Assets/Dialogs");
            phrasesFolderPath = EditorPrefs.GetString(PhrasesPathKey, "Assets/DialogPhrases");
        }

        private void OnGUI()
        {
            if (currentGraph == null)
            {
                DrawEmptyState();
                DrawControlsOverlay();
                return;
            }

            DrawGraphArea();
            DrawControlsOverlay();
        }

        private void DrawEmptyState()
        {
            Rect contentRect = new Rect(12f, 12f, position.width - 24f, 52f);
            GUI.Box(contentRect, GUIContent.none, EditorStyles.helpBox);
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
                if (GUI.Button(toggleRect, ">"))
                {
                    isControlsPanelExpanded = true;
                }

                return;
            }

            const float buttonHeight = 28f;
            float y = padding;

            EditorGUI.DrawRect(panelRect, new Color(0.18f, 0.18f, 0.18f, 1f));
            GUI.Box(panelRect, GUIContent.none, EditorStyles.helpBox);
            GUILayout.BeginArea(panelRect, GUIContent.none, EditorStyles.helpBox);
            float contentWidth = OverlayPanelWidth - padding * 2f;

            EditorGUI.LabelField(new Rect(padding, padding, contentWidth, 18f), "Dialogs Folder Path:");
            y += 18f;

            dialogsFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), dialogsFolderPath);
            if (GUI.Button(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Dialogs", ref dialogsFolderPath, DialogsPathKey);
            }

            if (GUI.Button(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                EditorPrefs.SetString(DialogsPathKey, dialogsFolderPath);
            }

            y += 28f;

            EditorGUI.LabelField(new Rect(padding, y, contentWidth, 18f), "Phrases Folder Path:");
            y += 18f;

            phrasesFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), phrasesFolderPath);
            if (GUI.Button(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Dialog Phrases", ref phrasesFolderPath, PhrasesPathKey);
            }

            if (GUI.Button(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                EditorPrefs.SetString(PhrasesPathKey, phrasesFolderPath);
            }

            y += 36f;

            if (GUI.Button(new Rect(padding, y, contentWidth, buttonHeight), "New Dialog"))
            {
                CreateNewGraph();
            }

            y += buttonHeight + spacing;

            if (GUI.Button(new Rect(padding, y, contentWidth, buttonHeight), "Load Dialog"))
            {
                LoadGraph();
            }

            y += buttonHeight + spacing;

            EditorGUI.BeginDisabledGroup(currentGraph == null);
            if (GUI.Button(new Rect(padding, y, contentWidth, buttonHeight), "New Phrase"))
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
                if (GUI.Button(new Rect(padding, y, contentWidth, buttonHeight), "Ping Entry Phrase"))
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
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUI.Button(new Rect(padding, y, contentWidth, buttonHeight), "Cancel Selection"))
                {
                    CancelTargetSelection();
                }

                GUI.backgroundColor = previousColor;
            }

            GUILayout.EndArea();

            if (GUI.Button(toggleRect, "<"))
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
            }
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
                Position = GetCenteredNodePosition(new Vector2(320f, 220f))
            };

            currentGraph.Nodes.Add(newNode);
            MarkDirty(currentGraph);

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
            CleanupGraph();
            answerAnchorPositions.Clear();
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

            DrawBackgroundGrid(new Rect(0f, 0f, WorkspaceWidth, WorkspaceHeight));

            EditorGUI.BeginDisabledGroup(isSelectingTargetPhrase);
            BeginWindows();

            for (int i = 0; i < currentGraph.Nodes.Count; i++)
            {
                DialogNode node = currentGraph.Nodes[i];
                Rect rect = new Rect(node.Position, new Vector2(320f, 220f));

                Color previousColor = GUI.color;
                GUI.color = GetNodeTint(node);
                rect = GUILayout.Window(i, rect, _ => DrawNodeWindow(node), GetNodeTitle(node));
                GUI.color = previousColor;

                nodeRects[node] = rect;
                node.Position = rect.position;
            }

            EndWindows();
            EditorGUI.EndDisabledGroup();

            DrawNodeMarkers();
            DrawConnections();
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
            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node.Phrase == null)
                {
                    continue;
                }

                Rect badgeRect = new Rect(node.Position.x + 6f, node.Position.y + 6f, 18f, 18f);
                Color previous = GUI.backgroundColor;

                if (currentGraph.IsEntryPhrase(node.Phrase))
                {
                    GUI.backgroundColor = new Color(0.2f, 0.7f, 0.25f);
                    GUI.Box(badgeRect, "S");
                }
                else if (IsOrphanPhrase(node.Phrase))
                {
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.15f);
                    GUI.Box(badgeRect, "!");
                }

                GUI.backgroundColor = previous;
            }
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();

            foreach (DialogNode node in currentGraph.Nodes)
            {
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

                    DialogNode targetNode = currentGraph.Nodes.FirstOrDefault(n => n.Phrase == answer.NextPhrase);
                    if (targetNode == null)
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

                    Handles.color = new Color(0.9f, 0.8f, 0.2f, 0.95f);
                    Vector2[] routePoints = BuildConnectionRoute(startPos, sourceRect, targetRect, nodeRects.Values);
                    DrawSmoothedConnection(routePoints);
                    DrawConnectionArrow(routePoints);
                }
            }

            Handles.EndGUI();
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
            Vector2 arrowBase1 = arrowTip - direction * 10f + perpendicular * 4f;
            Vector2 arrowBase2 = arrowTip - direction * 10f - perpendicular * 4f;
            Handles.DrawAAConvexPolygon(arrowTip, arrowBase1, arrowBase2);
        }

        private static void DrawSmoothedConnection(IReadOnlyList<Vector2> routePoints)
        {
            if (routePoints == null || routePoints.Count < 2)
            {
                return;
            }

            const float lineWidth = 2f;
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

        private void DrawTargetSelectionOverlay()
        {
            if (!isSelectingTargetPhrase || pendingAnswer == null)
            {
                return;
            }

            Handles.BeginGUI();

            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node.Phrase == null)
                {
                    continue;
                }

                if (!nodeRects.TryGetValue(node, out Rect rect))
                {
                    continue;
                }

                bool isHovered = rect.Contains(Event.current.mousePosition);
                EditorGUI.DrawRect(rect, new Color(0f, 0.75f, 0.2f, isHovered ? 0.45f : 0.25f));

                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 52,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                GUI.Label(rect, "+", style);

                if ((Event.current.rawType == EventType.MouseDown || Event.current.rawType == EventType.MouseUp) &&
                    rect.Contains(Event.current.mousePosition))
                {
                    pendingAnswer.SetNextPhrase(node.Phrase);
                    MarkDirty(sourcePhraseForSelection);
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

        private void DrawNodeWindow(DialogNode node)
        {
            if (TryHandleTargetPhraseSelection(node))
            {
                return;
            }

            EditorGUI.BeginDisabledGroup(isSelectingTargetPhrase);

            Rect removeButtonRect = new Rect(298f, 5f, 16f, 16f);
            if (GUI.Button(removeButtonRect, "x"))
            {
                DeleteNode(node);
                EditorGUI.EndDisabledGroup();
                return;
            }

            EditorGUI.BeginChangeCheck();
            var newPhrase = (DialogPhrase)EditorGUILayout.ObjectField(node.Phrase, typeof(DialogPhrase), false);
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

                    ReplacePhraseReferences(node.Phrase, newPhrase);
                    node.Phrase = newPhrase;
                    MarkDirty(currentGraph);
                }
            }

            if (node.Phrase == null)
            {
                EditorGUILayout.HelpBox("No phrase assigned.", MessageType.Warning);
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                EditorGUI.EndDisabledGroup();
                return;
            }

            DrawPhraseEditor(node.Phrase);

            if (GUILayout.Button(currentGraph.IsEntryPhrase(node.Phrase) ? "Start Phrase" : "Set As Start"))
            {
                currentGraph.SetEntryPhrase(node.Phrase);
                MarkDirty(currentGraph);
            }

            if (IsOrphanPhrase(node.Phrase))
            {
                EditorGUILayout.HelpBox(
                    "This phrase has no incoming answers and is not the entry phrase.",
                    MessageType.Warning);
            }

            EditorGUI.EndDisabledGroup();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
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

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Answers", EditorStyles.miniBoldLabel);

            int removeAnswerIndex = -1;
            DialogNode ownerNode = currentGraph.Nodes.FirstOrDefault(n => n.Phrase == phrase);

            for (int i = 0; i < answersProperty.arraySize; i++)
            {
                SerializedProperty answerProperty = answersProperty.GetArrayElementAtIndex(i);
                SerializedProperty answerTextProperty = answerProperty.FindPropertyRelative("text");
                SerializedProperty nextPhraseProperty = answerProperty.FindPropertyRelative("nextPhrase");

                bool missingLink = nextPhraseProperty.objectReferenceValue == null;
                bool targetOutsideGraph = nextPhraseProperty.objectReferenceValue != null &&
                                         !ContainsPhrase((DialogPhrase)nextPhraseProperty.objectReferenceValue);

                Color previousColor = GUI.color;
                if (missingLink)
                {
                    GUI.color = new Color(1f, 0.75f, 0.75f);
                }
                else if (targetOutsideGraph)
                {
                    GUI.color = new Color(1f, 0.9f, 0.65f);
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = previousColor;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Answer {i + 1}", EditorStyles.miniBoldLabel);

                if (GUILayout.Button("X", GUILayout.Width(22f)))
                {
                    removeAnswerIndex = i;
                }

                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = missingLink ? new Color(1f, 0.4f, 0.4f) : new Color(1f, 0.7f, 0.2f);
                bool pickPressed = GUILayout.Button("O", GUILayout.Width(22f));
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
                }

                EditorGUILayout.EndHorizontal();

                if (answerTextProperty != null)
                {
                    DrawLocalizedStringSelector(answerTextProperty, "Text");
                }

                if (nextPhraseProperty != null)
                {
                    EditorGUILayout.PropertyField(nextPhraseProperty, new GUIContent("Next Phrase"), true);
                }

                if (missingLink)
                {
                    EditorGUILayout.HelpBox("Next phrase is not assigned for this answer.", MessageType.Error);
                }
                else if (targetOutsideGraph)
                {
                    EditorGUILayout.HelpBox("Target phrase is not added to the current dialog graph.", MessageType.Warning);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }

            if (GUILayout.Button("+ Add Answer"))
            {
                answersProperty.arraySize++;
            }

            if (removeAnswerIndex >= 0)
            {
                answersProperty.DeleteArrayElementAtIndex(removeAnswerIndex);
            }

            if (phraseObject.hasModifiedProperties)
            {
                phraseObject.ApplyModifiedProperties();
                MarkDirty(phrase);
            }
        }

        private static void DrawLocalizedStringSelector(SerializedProperty localizedStringProperty, string label)
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
                return;
            }

            var collections = LocalizationEditorSettings.GetStringTableCollections();
            string currentTableValue = tableCollectionNameProperty.stringValue;
            int selectedCollectionIndex = GetSelectedCollectionIndex(collections, currentTableValue);

            string[] collectionOptions = new string[collections.Count + 1];
            collectionOptions[0] = "<None>";
            for (int i = 0; i < collections.Count; i++)
            {
                collectionOptions[i + 1] = collections[i].TableCollectionName;
            }

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            int newCollectionIndex = EditorGUILayout.Popup("Table", selectedCollectionIndex, collectionOptions);
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
            IReadOnlyList<SharedTableData.SharedTableEntry> entries = selectedCollection.SharedData.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();

            string[] entryOptions = new string[entries.Count + 1];
            entryOptions[0] = "<None>";
            for (int i = 0; i < entries.Count; i++)
            {
                entryOptions[i + 1] = entries[i].Key;
            }

            int selectedEntryIndex = GetSelectedEntryIndex(entries, keyIdProperty.longValue, keyProperty.stringValue);
            int newEntryIndex = EditorGUILayout.Popup("Entry", selectedEntryIndex, entryOptions);
            if (newEntryIndex != selectedEntryIndex)
            {
                ApplyEntrySelection(keyIdProperty, keyProperty, entries, newEntryIndex);
            }
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
                    if (string.Equals(entries[i].Key, keyName, StringComparison.Ordinal))
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

            if (GUILayout.Button(currentSelectionLabel, EditorStyles.popup))
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
                foreach (StringTableCollection collection in LocalizationEditorSettings.GetStringTableCollections())
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

        private void DeleteNode(DialogNode node)
        {
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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
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
                }
            }
        }

        private bool ContainsPhrase(DialogPhrase phrase)
        {
            return currentGraph.Nodes.Any(node => node.Phrase == phrase);
        }

        private bool IsOrphanPhrase(DialogPhrase phrase)
        {
            if (phrase == null || currentGraph.IsEntryPhrase(phrase))
            {
                return false;
            }

            return !currentGraph.Nodes
                .Where(node => node.Phrase != null)
                .SelectMany(node => node.Phrase.Answers)
                .Any(answer => answer != null && answer.NextPhrase == phrase);
        }

        private Color GetNodeTint(DialogNode node)
        {
            if (node.Phrase == null)
            {
                return Color.white;
            }

            if (currentGraph.IsEntryPhrase(node.Phrase))
            {
                return new Color(0.82f, 1f, 0.82f);
            }

            if (IsOrphanPhrase(node.Phrase))
            {
                return new Color(1f, 0.92f, 0.72f);
            }

            return Color.white;
        }

        private string GetNodeTitle(DialogNode node)
        {
            if (node.Phrase == null)
            {
                return "Phrase Node";
            }

            string prefix = currentGraph.IsEntryPhrase(node.Phrase) ? "[Start] " : string.Empty;
            return prefix + node.Phrase.name;
        }

        private void CancelTargetSelection(bool repaint = true)
        {
            isSelectingTargetPhrase = false;
            pendingAnswer = null;
            sourcePhraseForSelection = null;

            if (repaint)
            {
                Repaint();
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

        private void DrawBackgroundGrid(Rect rect)
        {
            Color minorColor = new Color(0.25f, 0.25f, 0.25f, 0.35f);
            Color majorColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);

            float gridSpacing = 20f * zoom;
            float majorStep = gridSpacing * 5f;
            Vector2 offset = new Vector2(panOffset.x % gridSpacing, panOffset.y % gridSpacing);

            Handles.BeginGUI();

            Handles.color = minorColor;
            for (float x = rect.xMin + offset.x; x < rect.xMax; x += gridSpacing)
            {
                Handles.DrawLine(new Vector3(x, rect.yMin, 0f), new Vector3(x, rect.yMax, 0f));
            }

            for (float y = rect.yMin + offset.y; y < rect.yMax; y += gridSpacing)
            {
                Handles.DrawLine(new Vector3(rect.xMin, y, 0f), new Vector3(rect.xMax, y, 0f));
            }

            Handles.color = majorColor;
            for (float x = rect.xMin + offset.x; x < rect.xMax; x += majorStep)
            {
                Handles.DrawLine(new Vector3(x, rect.yMin, 0f), new Vector3(x, rect.yMax, 0f));
            }

            for (float y = rect.yMin + offset.y; y < rect.yMax; y += majorStep)
            {
                Handles.DrawLine(new Vector3(rect.xMin, y, 0f), new Vector3(rect.xMax, y, 0f));
            }

            Handles.EndGUI();
        }
    }
}
