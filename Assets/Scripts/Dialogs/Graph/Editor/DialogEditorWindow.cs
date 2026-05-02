using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dialogs.Graph.Model;
using Quests.Editor;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Dialogs.Graph.Editor
{
    public class DialogEditorWindow : EditorWindow
    {
        private const string PreferredPreviewLocale = "ru";
        private const string DialogsPathKey = "DialogEditor_DialogsPath";
        private const string PhrasesPathKey = "DialogEditor_PhrasesPath";
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
        private readonly Dictionary<DialogPhrase, DialogNode> phraseToNodeLookup = new();
        private readonly HashSet<DialogPhrase> orphanPhrases = new();
        private readonly Dictionary<DialogPhrase, string> phraseDisplayNameCache = new();
        private readonly Dictionary<string, bool> answerFoldoutStates = new();
        private bool graphCachesDirty = true;
        private bool graphStructureDirty = true;
        private readonly Dictionary<DialogAnswer, CachedConnectionRoute> connectionRouteCache = new();
        private int cachedConnectionLayoutHash;

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
            EditorApplication.projectChanged += HandleProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
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

            answerAnchorPositions.Clear();
            nodeRects.Clear();
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
                DialogNode node = currentGraph.Nodes[i];
                Rect rect = new Rect(node.Position, new Vector2(DialogNodeWidth, 220f));

                Color previousColor = GUI.color;
                GUI.color = GetNodeTint(node);
                rect = GUILayout.Window(i, rect, _ => DrawNodeWindow(node), GetNodeTitle(node));
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
            graphCachesDirty = true;
            InvalidateConnectionRouteCache();
        }

        private void InvalidateGraphStructure()
        {
            graphStructureDirty = true;
            InvalidateGraphCaches();
        }

        private void InvalidateConnectionRouteCache()
        {
            connectionRouteCache.Clear();
            cachedConnectionLayoutHash = 0;
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

        private void DrawNodeMarkers()
        {
            foreach (KeyValuePair<DialogNode, Rect> pair in nodeRects)
            {
                DialogNode node = pair.Key;
                if (node.Phrase == null)
                {
                    continue;
                }

                Rect badgeRect = new Rect(pair.Value.x + 6f, pair.Value.y + 6f, 18f, 18f);
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

                    Handles.color = GetConnectionColor(node, targetNode);
                    Vector2 endPos = GetNearestSideCenter(targetRect, startPos);
                    (Vector2 startTangent, Vector2 endTangent) = ResolveConnectionTangents(
                        startPos,
                        endPos,
                        sourceRect,
                        targetRect,
                        nodeRects
                            .Where(item => item.Key != node && item.Key != targetNode)
                            .Select(item => ExpandRect(item.Value, 8f))
                            .ToList());

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
                return new Color(0.9f, 0.8f, 0.2f, 0.95f);
            }

            if (sourceNode == activeConnectionNode)
            {
                return new Color(1f, 1f, 1f, 0.98f);
            }

            if (targetNode == activeConnectionNode)
            {
                return new Color(1f, 0.28f, 0.28f, 0.98f);
            }

            return new Color(0.9f, 0.8f, 0.2f, 0.95f);
        }

        private int ComputeConnectionLayoutHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (KeyValuePair<DialogNode, Rect> pair in nodeRects.OrderBy(item => item.Key.Phrase != null ? item.Key.Phrase.GetInstanceID() : 0))
                {
                    Rect rect = pair.Value;
                    hash = hash * 31 + Mathf.RoundToInt(rect.x * 10f);
                    hash = hash * 31 + Mathf.RoundToInt(rect.y * 10f);
                    hash = hash * 31 + Mathf.RoundToInt(rect.width * 10f);
                    hash = hash * 31 + Mathf.RoundToInt(rect.height * 10f);
                }

                return hash;
            }
        }

        private Vector2[] GetOrBuildConnectionRoute(
            DialogAnswer answer,
            Vector2 startPos,
            Rect sourceRect,
            Rect targetRect,
            IReadOnlyList<Rect> expandedNodeRects)
        {
            if (answer != null &&
                connectionRouteCache.TryGetValue(answer, out CachedConnectionRoute cachedRoute) &&
                cachedRoute.LayoutHash == cachedConnectionLayoutHash &&
                ApproximatelyEqual(cachedRoute.StartPos, startPos) &&
                RectApproximatelyEqual(cachedRoute.SourceRect, sourceRect) &&
                RectApproximatelyEqual(cachedRoute.TargetRect, targetRect))
            {
                return cachedRoute.RoutePoints;
            }

            Vector2[] routePoints = BuildConnectionRoute(startPos, sourceRect, targetRect, expandedNodeRects);
            if (answer != null)
            {
                connectionRouteCache[answer] = new CachedConnectionRoute(
                    cachedConnectionLayoutHash,
                    startPos,
                    sourceRect,
                    targetRect,
                    routePoints);
            }

            return routePoints;
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
            Vector2 arrowBase = tipPosition - normalizedDirection * 14f;
            Vector3[] arrow =
            {
                tipPosition,
                arrowBase + right * 6f,
                arrowBase - right * 6f
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
            }

            public IReadOnlyList<SharedTableData.SharedTableEntry> Entries { get; }
            public string[] Options { get; }
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

        private readonly struct CachedConnectionRoute
        {
            public CachedConnectionRoute(int layoutHash, Vector2 startPos, Rect sourceRect, Rect targetRect, Vector2[] routePoints)
            {
                LayoutHash = layoutHash;
                StartPos = startPos;
                SourceRect = sourceRect;
                TargetRect = targetRect;
                RoutePoints = routePoints;
            }

            public int LayoutHash { get; }
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

        private void DrawTargetSelectionOverlay()
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

                if (!nodeRects.TryGetValue(node, out Rect rect))
                {
                    continue;
                }

                bool isHovered = rect.Contains(graphMousePosition);
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

        private void DrawNodeWindow(DialogNode node)
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
                Repaint();
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
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                EditorGUI.EndDisabledGroup();
                return;
            }

            DrawPhraseEditor(node.Phrase);

            if (GUILayout.Button(currentGraph.IsEntryPhrase(node.Phrase) ? "Start Phrase" : "Set As Start"))
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
            EditorGUILayout.LabelField("Answers", EditorStyles.miniBoldLabel);

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

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

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
                bool newExpanded = EditorGUILayout.Foldout(isExpanded, $"Answer {i + 1}", true);
                if (newExpanded != isExpanded)
                {
                    SetAnswerFoldoutState(foldoutKey, newExpanded);
                    isExpanded = newExpanded;
                }

                GUILayout.Label(statusLabel, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(110f));
                GUILayout.FlexibleSpace();

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

                if (isExpanded)
                {
                    EditorGUILayout.EndHorizontal();
                    DrawAnswerDivider(new Color(1f, 1f, 1f, 0.12f));
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
                    DrawAnswerDivider(new Color(1f, 1f, 1f, 0.08f));
                    EditorGUILayout.Space(5f);
                }
                else
                {
                    EditorGUILayout.Space(4f);
                }
            }

            if (GUILayout.Button("+ Add Answer"))
            {
                answersProperty.arraySize++;
                ClearAnswerFoldoutStates(phrase);
            }

            if (removeAnswerIndex >= 0)
            {
                answersProperty.DeleteArrayElementAtIndex(removeAnswerIndex);
                ClearAnswerFoldoutStates(phrase);
            }

            if (phraseObject.hasModifiedProperties)
            {
                phraseObject.ApplyModifiedProperties();
                MarkDirty(phrase);
                InvalidatePhraseDisplayName(phrase);
                InvalidateGraphCaches();
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
            EditorGUILayout.LabelField("Quest Entry Answer", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "This answer is shown on the start phrase automatically when its conditions are satisfied.",
                EditorStyles.wordWrappedMiniLabel);
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

            answerFoldoutStates[foldoutKey] = true;
            return true;
        }

        private void SetAnswerFoldoutState(string foldoutKey, bool isExpanded)
        {
            answerFoldoutStates[foldoutKey] = isExpanded;
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

        private static Color GetAnswerAccentColor(bool missingLink, bool targetOutsideGraph, bool hasConditions)
        {
            if (missingLink)
            {
                return new Color(0.92f, 0.34f, 0.34f, 1f);
            }

            if (targetOutsideGraph)
            {
                return new Color(0.95f, 0.66f, 0.22f, 1f);
            }

            if (hasConditions)
            {
                return new Color(0.26f, 0.63f, 0.86f, 1f);
            }

            return new Color(0.38f, 0.78f, 0.42f, 1f);
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

        private static void DrawAnswerDivider(Color color)
        {
            Rect dividerRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(dividerRect, color);
        }

        private static Color GetConditionAccentColor(DialogAnswerConditionType conditionType)
        {
            return new Color(0.24f, 0.78f, 0.76f, 1f);
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
            EditorGUILayout.LabelField("Conditions / Actions", EditorStyles.miniBoldLabel);

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

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
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
                EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
                GUILayout.Label(conditionTitle, EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(22f)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();
                DrawAnswerDivider(new Color(1f, 1f, 1f, 0.10f));
                EditorGUILayout.Space(3f);

                EditorGUILayout.PropertyField(typeProperty, new GUIContent("Type"));

                switch (conditionType)
                {
                    case DialogAnswerConditionType.GiveMoney:
                    case DialogAnswerConditionType.TakeMoney:
                    case DialogAnswerConditionType.TakeMoneyMax:
                        EditorGUILayout.PropertyField(moneyAmountProperty, new GUIContent("Money"));
                        break;
                    case DialogAnswerConditionType.TakeItemIfHas:
                        EditorGUILayout.PropertyField(itemConfigProperty, new GUIContent("Item"));
                        EditorGUILayout.PropertyField(itemCountProperty, new GUIContent("Count"));
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
                    DrawAnswerDivider(new Color(1f, 1f, 1f, 0.06f));
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

            if (GUILayout.Button("+ Add Condition / Action"))
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
            EditorGUILayout.LabelField("Quest Links", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (string line in lines)
            {
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
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
            if (GUI.Button(selectorRect, buttonLabel, EditorStyles.popup))
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

            EditorGUILayout.LabelField("Used In This Answer", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (QuestGraph relatedGraph in relatedGraphs)
            {
                string label = QuestPreviewUtility.GetQuestDisplayName(relatedGraph);
                if (GUILayout.Button(label, EditorStyles.miniButton))
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
            if (GUI.Button(sourceNodeRect, sourceNodeLabel, EditorStyles.popup))
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
            if (GUI.Button(transitionRect, transitionLabel, EditorStyles.popup))
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
            if (GUI.Button(terminalNodeRect, terminalNodeLabel, EditorStyles.popup))
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
                DrawLocalizedStringPreview(localizedStringProperty);
                return;
            }

            var collections = GetCachedStringTableCollections();
            string currentTableValue = tableCollectionNameProperty.stringValue;
            int selectedCollectionIndex = GetSelectedCollectionIndex(collections, currentTableValue);

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            int newCollectionIndex = EditorGUILayout.Popup("Table", selectedCollectionIndex, GetCachedStringTableOptions());
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
            if (EditorGUI.DropdownButton(entryRect, new GUIContent($"Entry: {currentEntryLabel}"), FocusType.Passive))
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

        private static void DrawLocalizedStringPreview(SerializedProperty localizedStringProperty)
        {
            string previewText = GetLocalizedStringPreview(localizedStringProperty, PreferredPreviewLocale);
            if (string.IsNullOrWhiteSpace(previewText))
            {
                return;
            }

            EditorGUILayout.LabelField("RU Preview", EditorStyles.miniBoldLabel);

            GUIStyle previewStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };

            float width = LocalizedPreviewWidth;
            float height = Mathf.Max(LocalizedPreviewMinHeight, previewStyle.CalcHeight(new GUIContent(previewText), width));

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(
                previewText,
                previewStyle,
                GUILayout.MinHeight(LocalizedPreviewMinHeight),
                GUILayout.Height(height));
            EditorGUI.EndDisabledGroup();
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
                if (string.Equals(serializedTableReference, guidReference, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(serializedTableReference, collection.TableCollectionName, StringComparison.Ordinal))
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

        private void DeleteNode(DialogNode node)
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
                return new Color(0.82f, 1f, 0.82f);
            }

            if (node.Phrase.IsQuestPhrase)
            {
                return new Color(0.80f, 0.90f, 1f);
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
